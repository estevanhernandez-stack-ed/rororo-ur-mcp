using System.IO.Pipes;
using System.Security.Cryptography;
using Grpc.Core;
using Grpc.Net.Client;
using ROROROblox.PluginContract;

namespace Labs626.UrMcp.Ipc;

/// <summary>
/// The real gRPC client of RoRoRo's plugin host over the per-user named pipe. Authority comes
/// from being an INSTALLED, CONSENTED plugin — the host's handshake authenticates against the
/// installed registry, and every gated call is checked by the host's CapabilityInterceptor
/// against what the user granted at install. Claude spawns this process; RoRoRo does not.
/// <para>
/// Two wire details are load-bearing and easy to lose. (1) Every call after the handshake
/// carries the <c>x-plugin-id</c> metadata header — the interceptor resolves the caller from
/// that header, and without it every gated RPC fails FailedPrecondition ("Handshake required").
/// (2) The channel connects lazily and the handshake runs once per process; a dead pipe at any
/// point surfaces as <see cref="HostUnavailableException"/> with the message the tools show.
/// </para>
/// </summary>
public sealed class RoRoRoHostClient : IRoRoRoHost, IDisposable
{
    public const string PluginId = "626labs.ur-mcp";
    public const string PipeName = "rororo-plugin-host";
    private const string ContractVersion = "1.0";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    private readonly string _pipeName;
    private readonly SemaphoreSlim _handshakeGate = new(1, 1);
    private GrpcChannel? _channel;
    private RoRoRoHost.RoRoRoHostClient? _client;
    private bool _handshaken;

    public RoRoRoHostClient(string? pipeName = null) => _pipeName = pipeName ?? PipeName;

    public async Task<HostInfoDto> GetHostInfoAsync(CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var info = await Invoke(() => client.GetHostInfoAsync(new Empty(), Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return new HostInfoDto(info.Version, info.MultiInstanceEnabled, info.MultiInstanceState);
    }

    public async Task<IReadOnlyList<HostAccount>> GetAccountsAsync(CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var list = await Invoke(() => client.GetAccountsAsync(new Empty(), Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return list.Accounts
            .Select(a => new HostAccount(a.AccountId, a.RobloxUserId, a.DisplayName, a.IsMain))
            .ToList();
    }

    public async Task<IReadOnlyList<RunningAccountDto>> GetRunningAccountsAsync(CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var list = await Invoke(() => client.GetRunningAccountsAsync(new Empty(), Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return list.Accounts
            .Select(a => new RunningAccountDto(a.AccountId, a.RobloxUserId, a.DisplayName, a.ProcessId, a.PlaceId, a.PlaceName))
            .ToList();
    }

    public async Task<CurrentServerInfo> GetCurrentServerAsync(CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var server = await Invoke(() => client.GetCurrentServerAsync(new Empty(), Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return new CurrentServerInfo(server.Present, server.ShareUrl);
    }

    public async Task<IReadOnlyList<AccountActivityDto>> GetAccountActivityAsync(CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var list = await Invoke(() => client.GetAccountActivityAsync(new Empty(), Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return list.Items
            .Select(a => new AccountActivityDto(a.AccountId, a.LastActivityUnixMs, a.SecondsSinceActivity))
            .ToList();
    }

    public async Task<LaunchOutcome> RequestLaunchAsync(string accountId, CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var result = await Invoke(() => client.RequestLaunchAsync(
            new LaunchRequest { AccountId = accountId }, Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return new LaunchOutcome(result.Ok, string.IsNullOrEmpty(result.FailureReason) ? null : result.FailureReason);
    }

    public async Task<LaunchOutcome> LaunchTargetShareAsync(string accountId, string shareUrl, CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var result = await Invoke(() => client.RequestLaunchTargetAsync(
            new LaunchTargetRequest { AccountId = accountId, ShareUrl = shareUrl }, Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return new LaunchOutcome(result.Ok, string.IsNullOrEmpty(result.FailureReason) ? null : result.FailureReason);
    }

    public async Task<LaunchOutcome> LaunchTargetFollowAsync(string accountId, long followUserId, CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var result = await Invoke(() => client.RequestLaunchTargetAsync(
            new LaunchTargetRequest { AccountId = accountId, FollowUserId = followUserId }, Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return new LaunchOutcome(result.Ok, string.IsNullOrEmpty(result.FailureReason) ? null : result.FailureReason);
    }

    public async Task<StopOutcome> StopAccountsAsync(IReadOnlyList<string> accountIds, CancellationToken ct = default)
    {
        var client = await EnsureHandshakeAsync(ct).ConfigureAwait(false);
        var request = new StopAccountsRequest();
        request.AccountIds.AddRange(accountIds);
        var result = await Invoke(() => client.StopAccountsAsync(request, Headers(), cancellationToken: ct).ResponseAsync).ConfigureAwait(false);
        return new StopOutcome(result.StoppedCount, result.FailedAccountIds.ToList());
    }

    /// <summary>Every post-handshake call carries the plugin id — see the class doc.</summary>
    private static Metadata Headers() => new() { { "x-plugin-id", PluginId } };

    private async Task<RoRoRoHost.RoRoRoHostClient> EnsureHandshakeAsync(CancellationToken ct)
    {
        if (_handshaken && _client is not null) return _client;

        await _handshakeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_handshaken && _client is not null) return _client;

            _channel ??= GrpcChannel.ForAddress("http://pipe", new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    ConnectCallback = async (ctx, connectCt) =>
                    {
                        var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connectCt);
                        timeout.CancelAfter(ConnectTimeout);
                        try
                        {
                            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            pipe.Dispose();
                            throw new HostUnavailableException(
                                "RoRoRo isn't running — open it and try again.", ex);
                        }
                        return pipe;
                    },
                },
            });
            _client ??= new RoRoRoHost.RoRoRoHostClient(_channel);

            var response = await Invoke(() => _client.HandshakeAsync(new HandshakeRequest
            {
                PluginId = PluginId,
                ManifestSha256 = TryReadOwnManifestSha(),
                ContractVersion = ContractVersion,
            }, cancellationToken: ct).ResponseAsync).ConfigureAwait(false);

            if (!response.Accepted)
            {
                throw new HostUnavailableException(
                    $"RoRoRo refused the connection: {response.RejectReason}. " +
                    "Is the Ur MCP plugin installed in RoRoRo's Plugins window?");
            }

            _handshaken = true;
            return _client;
        }
        finally
        {
            _handshakeGate.Release();
        }
    }

    /// <summary>
    /// SHA-256 of the manifest beside the exe. The host ignores this field today; sent for
    /// completeness so a future host that checks it finds the honest value.
    /// </summary>
    private static string TryReadOwnManifestSha()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "manifest.json");
            if (!File.Exists(path)) return string.Empty;
            var bytes = SHA256.HashData(File.ReadAllBytes(path));
            return Convert.ToHexStringLower(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// One error-translation seam for every RPC — the tools show these messages verbatim.
    /// </summary>
    private static async Task<T> Invoke<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied)
        {
            throw new ConsentDeniedException(
                $"{ex.Status.Detail} Grant it in RoRoRo's Plugins window, then try again.");
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.Internal)
        {
            throw new HostUnavailableException("RoRoRo isn't running — open it and try again.", ex);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            // The host's "not wired / not ready" answers (theme not applied, saved accounts not
            // available, handshake-required). Surfaced with the host's own words.
            throw new HostUnavailableException($"RoRoRo declined the call: {ex.Status.Detail}", ex);
        }
        catch (HostUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or TimeoutException)
        {
            throw new HostUnavailableException("RoRoRo isn't running — open it and try again.", ex);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _handshakeGate.Dispose();
    }
}
