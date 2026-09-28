using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Labs626.UrMcp.Ipc;

namespace Labs626.UrMcp.Tests;

/// <summary>
/// Pins this end of the GetPlayback wire over a real pipe: the request shape Ur Task will parse,
/// and the reply shapes it sends back — including 0.8.0's unknown-method refusal, which carries
/// RunMacroResponse's fields (no state), not GetPlayback's.
/// </summary>
public class UrTaskBridgeClientTests
{
    [Fact]
    public async Task GetPlayback_SendsTheContractShape_AndReadsTheReply()
    {
        var (request, result) = await ExchangeOnceAsync(
            """{"ok":true,"state":"failed","reason":"check-failed","detail":"step 3 missed","stepIndex":3}""",
            c => c.GetPlaybackAsync("pb-7"));

        Assert.Equal("1.0", request.GetProperty("contractVersion").GetString());
        Assert.Equal("GetPlayback", request.GetProperty("method").GetString());
        Assert.Equal("pb-7", request.GetProperty("playbackId").GetString());
        Assert.Equal("626labs.ur-mcp", request.GetProperty("callerPluginId").GetString());

        Assert.Equal(new BridgePlaybackResult(true, "failed", "check-failed", "step 3 missed", 3), result);
    }

    [Fact]
    public async Task GetPlayback_AgainstUrTask080_ParsesTheUnknownMethodRefusal()
    {
        var (_, result) = await ExchangeOnceAsync(
            """{"ok":false,"playbackId":null,"queued":false,"reason":"refused","detail":"Unknown method 'GetPlayback'."}""",
            c => c.GetPlaybackAsync("pb-7"));

        Assert.False(result.Ok);
        Assert.Null(result.State);
        Assert.Equal("refused", result.Reason);
        Assert.Equal("Unknown method 'GetPlayback'.", result.Detail);
    }

    private static async Task<(JsonElement Request, T Result)> ExchangeOnceAsync<T>(
        string replyJson, Func<UrTaskBridgeClient, Task<T>> call)
    {
        var pipeName = $"ur-mcp-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var serve = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            var frame = await FrameCodec.ReadFrameAsync(server, default);
            await FrameCodec.WriteFrameAsync(server, Encoding.UTF8.GetBytes(replyJson), default);
            return JsonDocument.Parse(frame!).RootElement.Clone();
        });

        var result = await call(new UrTaskBridgeClient(pipeName));
        return (await serve, result);
    }
}
