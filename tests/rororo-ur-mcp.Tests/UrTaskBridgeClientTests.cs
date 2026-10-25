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

    [Fact]
    public async Task GetStatus_SendsTheContractShape_AndReadsTheReply()
    {
        var (request, result) = await ExchangeOnceAsync(
            """{"ok":true,"keepAlive":{"state":"running","turbo":true},"accounts":[{"userId":"100","name":"Pokey","role":"keep-alive","idleSeconds":360,"deadlineAt":"2026-10-25T15:42:00-05:00","limitMinutes":20,"misses":0,"state":"waiting"}],"playbacks":[{"playbackId":"pb-1","paused":true,"pausedFor":"Pokey"}]}""",
            c => c.GetStatusAsync());

        Assert.Equal("1.0", request.GetProperty("contractVersion").GetString());
        Assert.Equal("GetStatus", request.GetProperty("method").GetString());
        Assert.Equal("626labs.ur-mcp", request.GetProperty("callerPluginId").GetString());

        Assert.True(result.Ok);
        Assert.Equal(new BridgeKeepAlive("running", true), result.KeepAlive);
        var a = Assert.Single(result.Accounts!);
        Assert.Equal("Pokey", a.Name);
        Assert.Equal(360, a.IdleSeconds);
        Assert.Null(a.LastMiss);
        Assert.Equal("2026-10-25T15:42:00-05:00", a.DeadlineAt);
        Assert.Equal(new BridgePlaybackStatus("pb-1", true, "Pokey"), Assert.Single(result.Playbacks!));
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
