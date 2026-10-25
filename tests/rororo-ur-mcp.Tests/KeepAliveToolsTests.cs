using Labs626.UrMcp.Ipc;
using Labs626.UrMcp.Tools;

namespace Labs626.UrMcp.Tests;

public class KeepAliveToolsTests
{
    // Built with the local offset so the expected HH:mm holds in any time zone.
    private static readonly DateTimeOffset Deadline = new(
        2026, 10, 25, 15, 42, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 25, 15, 42, 0)));

    private static BridgeAccountStatus Account(
        string name, string role = "keep-alive", int? idle = 360, DateTimeOffset? deadline = null,
        int misses = 0, string? lastMiss = null)
        => new("100", name, role, null, null, idle, deadline?.ToString("O"), 20, misses, lastMiss, "waiting");

    private static FakeBridge Bridge(string kaState, bool turbo, params BridgeAccountStatus[] accounts)
        => new() { Status = new(true, new BridgeKeepAlive(kaState, turbo), accounts, [], null, null) };

    [Fact]
    public async Task RendersOneLinePerAccount_WithIdleDeadlineAndMisses()
    {
        var bridge = Bridge("running", true,
            Account("Pokey", deadline: Deadline),
            Account("Clover", idle: 125, deadline: Deadline, misses: 2));

        var text = await KeepAliveTools.KeepAliveStatus(bridge);

        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal("Keep-alive is running (turbo on).", lines[0]);
        Assert.Equal("Pokey: idle 6m, needs input by 15:42 (keep-alive), misses 0", lines[1]);
        Assert.Equal("Clover: idle 2m, needs input by 15:42 (keep-alive), misses 2", lines[2]);
    }

    [Fact]
    public async Task OffAccount_SaysKeepAliveOff()
    {
        var text = await KeepAliveTools.KeepAliveStatus(
            Bridge("running", false, Account("Syn", role: "off", idle: 360)));

        Assert.Contains("Syn: keep-alive off, idle 6m", text);
        Assert.DoesNotContain("needs input", text);
    }

    [Fact]
    public async Task UnknownIdle_RendersIdleUnknown_AndLastMissIsAppended()
    {
        var text = await KeepAliveTools.KeepAliveStatus(Bridge("running", false,
            Account("Pokey", idle: null, deadline: Deadline, misses: 1, lastMiss: "window not found")));

        Assert.Contains(
            "Pokey: idle unknown, needs input by 15:42 (keep-alive), misses 1 — last miss: window not found", text);
    }

    [Fact]
    public async Task StoppedAndBreakGlass_HeadlineStates()
    {
        Assert.StartsWith("Keep-alive is stopped.", await KeepAliveTools.KeepAliveStatus(Bridge("stopped", false)));
        Assert.StartsWith("Keep-alive is paused: break-glass is on.", await KeepAliveTools.KeepAliveStatus(Bridge("breakglass", false)));
    }

    [Fact]
    public async Task PausedPlayback_IsNamed()
    {
        var bridge = new FakeBridge
        {
            Status = new(true, new BridgeKeepAlive("running", false), [],
                [new BridgePlaybackStatus("pb-3", true, "Pokey"), new BridgePlaybackStatus("pb-4", false, null)], null, null),
        };

        var text = await KeepAliveTools.KeepAliveStatus(bridge);

        Assert.Contains("Macro pb-3 is paused for Pokey.", text);
        Assert.DoesNotContain("pb-4", text);
    }

    [Fact]
    public async Task UnknownMethod_TellsTheUserToUpdateUrTask()
    {
        var bridge = new FakeBridge { Status = new(false, null, null, null, "refused", "Unknown method 'GetStatus'.") };

        Assert.Equal("Update Ur Task to 0.13.0 or later to see keep-alive status.", await KeepAliveTools.KeepAliveStatus(bridge));
    }

    [Fact]
    public async Task OtherRefusal_UsesTheStandardRefusalFormat()
    {
        var bridge = new FakeBridge { Status = new(false, null, null, null, "busy", "try later") };

        Assert.Equal("Ur Task refused: busy — try later", await KeepAliveTools.KeepAliveStatus(bridge));
    }
}
