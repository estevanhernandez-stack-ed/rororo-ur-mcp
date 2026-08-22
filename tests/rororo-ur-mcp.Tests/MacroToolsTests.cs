using Labs626.UrMcp.Ipc;
using Labs626.UrMcp.Tools;

namespace Labs626.UrMcp.Tests;

public class MacroToolsTests
{
    private static readonly HostAccount Pokey = new("id-pokey", 111, "Pokey", IsMain: true);
    private static readonly HostAccount Clover = new("id-clover", 0, "Clover", IsMain: false);

    [Fact]
    public async Task RunMacro_ResolvesMacroAndTargets_ToUidStrings()
    {
        var host = new FakeHost { Accounts = [Pokey] };
        var bridge = new FakeBridge { Macros = [new("m-1", "Farm")] };

        var text = await MacroTools.RunMacro(host, bridge, "farm", ["Pokey"], repeat: true);

        Assert.NotNull(bridge.LastRun);
        Assert.Equal("m-1", bridge.LastRun!.Value.MacroId);
        Assert.Equal(new[] { "111" }, bridge.LastRun.Value.Targets);
        Assert.True(bridge.LastRun.Value.Repeat);
        Assert.Contains("on repeat", text);
        Assert.Contains("pb-1", text);
    }

    [Fact]
    public async Task RunMacro_NoTargets_DefaultsToForeground()
    {
        var bridge = new FakeBridge { Macros = [new("m-1", "Farm")] };

        var text = await MacroTools.RunMacro(new FakeHost(), bridge, "Farm");

        Assert.Equal(new[] { "foreground" }, bridge.LastRun!.Value.Targets);
        Assert.Contains("foreground window", text);
    }

    [Fact]
    public async Task RunMacro_TargetWithUnresolvedUid_GetsTheFixInstruction()
    {
        var host = new FakeHost { Accounts = [Clover] };
        var bridge = new FakeBridge { Macros = [new("m-1", "Farm")] };

        var text = await MacroTools.RunMacro(host, bridge, "Farm", ["Clover"]);

        Assert.Null(bridge.LastRun); // never reached the bridge
        Assert.Contains("no resolved Roblox user id", text);
        Assert.Contains("launch it once first", text);
    }

    [Fact]
    public async Task RunMacro_BusyRefusal_SurfacesVerbatim()
    {
        var bridge = new FakeBridge
        {
            Macros = [new("m-1", "Farm")],
            RunResult = new(false, null, "busy", "A sequence is already running."),
        };

        var text = await MacroTools.RunMacro(new FakeHost(), bridge, "Farm");

        Assert.Contains("busy", text);
        Assert.Contains("A sequence is already running.", text);
    }

    [Fact]
    public async Task StopMacro_ById_AndStopAll()
    {
        var bridge = new FakeBridge { StopResult = new(true, 1, null, null) };
        var text = await MacroTools.StopMacro(bridge, "pb-9");
        Assert.Equal("pb-9", bridge.LastStopId);
        Assert.Contains("Stopped 1", text);

        var bridge2 = new FakeBridge { StopResult = new(true, 0, null, null) };
        var text2 = await MacroTools.StopMacro(bridge2);
        Assert.True(bridge2.StopCalled);
        Assert.Null(bridge2.LastStopId);
        Assert.Contains("Nothing was running", text2);
    }

    [Fact]
    public async Task BridgeDown_IsAReadableAnswer()
    {
        var bridge = new FakeBridge { Throw = new BridgeUnavailableException("Ur Task isn't available — is it installed and running in RoRoRo?") };
        var text = await MacroTools.ListMacros(bridge);
        Assert.Contains("Ur Task isn't available", text);
    }
}
