using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Labs626.UrMcp.Ipc;

/// <summary>
/// Client of Ur Task's action bridge: one connect → one length-prefixed JSON request → one
/// response → close, per call — the same one-shot shape Ur OCR already uses. The pipe is
/// same-Windows-user by construction; the input-synthesis consent lives in UR TASK's own
/// capability grants, never here — this client only ever ASKS Ur Task to run what the user
/// already recorded and consented to.
/// </summary>
public sealed class UrTaskBridgeClient : IUrTaskBridge
{
    public const string PipeName = "626labs-ur-task";
    private const string ContractVersion = "1.0";
    private const string CallerPluginId = "626labs.ur-mcp";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _pipeName;

    public UrTaskBridgeClient(string? pipeName = null) => _pipeName = pipeName ?? PipeName;

    private sealed record ListMacrosWire(string ContractVersion, string Method, string CallerPluginId);
    private sealed record RunMacroWire(
        string ContractVersion, string Method, string MacroId, IReadOnlyList<string>? Targets,
        int? InterAltDelayMs, string CallerPluginId, bool Repeat);
    private sealed record StopMacroWire(
        string ContractVersion, string Method, string? PlaybackId, IReadOnlyList<string>? Targets, string CallerPluginId);

    private sealed record ListMacrosReply(bool Ok, IReadOnlyList<BridgeMacro>? Macros, string? Reason, string? Detail);
    private sealed record RunMacroReply(bool Ok, string? PlaybackId, bool Queued, string? Reason, string? Detail);
    private sealed record StopMacroReply(bool Ok, int Stopped, string? Reason, string? Detail);

    public async Task<IReadOnlyList<BridgeMacro>> ListMacrosAsync(CancellationToken ct = default)
    {
        var reply = await ExchangeAsync<ListMacrosWire, ListMacrosReply>(
            new ListMacrosWire(ContractVersion, "ListMacros", CallerPluginId), ct).ConfigureAwait(false);
        if (!reply.Ok)
            throw new BridgeUnavailableException($"Ur Task refused ListMacros: {reply.Reason} {reply.Detail}".Trim());
        return reply.Macros ?? [];
    }

    public async Task<BridgeRunResult> RunMacroAsync(
        string macroId, IReadOnlyList<string> targets, bool repeat, CancellationToken ct = default)
    {
        var reply = await ExchangeAsync<RunMacroWire, RunMacroReply>(
            new RunMacroWire(ContractVersion, "RunMacro", macroId, targets, null, CallerPluginId, repeat), ct)
            .ConfigureAwait(false);
        return new BridgeRunResult(reply.Ok, reply.PlaybackId, reply.Reason, reply.Detail);
    }

    public async Task<BridgeStopResult> StopMacroAsync(string? playbackId, CancellationToken ct = default)
    {
        var reply = await ExchangeAsync<StopMacroWire, StopMacroReply>(
            new StopMacroWire(ContractVersion, "StopMacro", playbackId, null, CallerPluginId), ct).ConfigureAwait(false);
        return new BridgeStopResult(reply.Ok, reply.Stopped, reply.Reason, reply.Detail);
    }

    private async Task<TReply> ExchangeAsync<TRequest, TReply>(TRequest request, CancellationToken ct)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(ConnectTimeout);
                await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(request, Json);
            await FrameCodec.WriteFrameAsync(pipe, payload, ct).ConfigureAwait(false);
            var replyBytes = await FrameCodec.ReadFrameAsync(pipe, ct).ConfigureAwait(false)
                ?? throw new BridgeUnavailableException("Ur Task closed the connection without answering.");

            return JsonSerializer.Deserialize<TReply>(replyBytes, Json)
                ?? throw new BridgeUnavailableException("Ur Task sent an empty reply.");
        }
        catch (BridgeUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new BridgeUnavailableException(
                "Ur Task isn't available — is it installed and running in RoRoRo?");
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or JsonException or EndOfStreamException)
        {
            throw new BridgeUnavailableException(
                "Ur Task isn't available — is it installed and running in RoRoRo?", ex);
        }
    }
}
