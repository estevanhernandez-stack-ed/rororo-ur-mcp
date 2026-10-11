using Labs626.UrMcp.Ipc;
using Labs626.UrMcp.Tools;

namespace Labs626.UrMcp.Tests;

public class MacroNamingTests
{
    private const string Booth = "PLZ | booth 1/3 | Make Listing + paste search + pick + Confirm (clipboard = name)";
    private const string Trade = "ANY | trade 2/7 | list search: ELeonDog (rewritten per run)";
    private const string Move = "ANY | move W 250ms";
    private const string Egg = "HW Z4 | egg 3/6 | walk to boss pad + fight, tap ~11s [CECPapa] (from button arrival)";
    private const string Crystal = "Crystal";
    private const string Trader = "PLZ | trader 2/4 | Pro spawn -> level 2 booth (Este walk)";

    private static BridgeMacro M(string name) => new(name.GetHashCode().ToString(), name);
    private static readonly BridgeMacro[] All = new[] { Booth, Trade, Move, Egg, Crystal, Trader }.Select(M).ToArray();

    [Theory]
    [InlineData(Booth, "PLZ", "booth", 1)]
    [InlineData(Trade, "ANY", "trade", 2)]
    [InlineData(Egg, "HW Z4", "egg", 3)]
    [InlineData(Trader, "PLZ", "trader", 2)]
    public void Parse_RouteSteps(string name, string group, string route, int step)
    {
        var p = MacroNaming.Parse(name);
        Assert.Equal(group, p.Group);
        Assert.Equal(route, p.Route);
        Assert.Equal(step, p.Step);
    }

    [Fact]
    public void Parse_GroupedWithoutRoute_AndUngrouped()
    {
        var move = MacroNaming.Parse(Move);
        Assert.Equal("ANY", move.Group);
        Assert.Null(move.Step);
        Assert.Equal(MacroNaming.Ungrouped, MacroNaming.Parse(Crystal).Group);
    }

    [Fact]
    public void Parse_DoubleStepUsesFirstNumber()
        => Assert.Equal(2, MacroNaming.Parse("PLZ | trade 2/6 + 4/6 | both halves").Step);

    [Theory]
    [InlineData("booth confirm", true)]
    [InlineData("BOOTH   CONFIRM", true)]
    [InlineData("booth zebra", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void Query_EveryWordMustMatch(string? query, bool expected)
        => Assert.Equal(expected, MacroNaming.MatchesQuery(Booth, query));

    [Fact]
    public void Group_IsExactAndCaseInsensitive()
    {
        Assert.True(MacroNaming.MatchesGroup(Booth, "plz"));
        Assert.False(MacroNaming.MatchesGroup(Booth, "PL"));
        Assert.True(MacroNaming.MatchesGroup(Crystal, "ungrouped"));
    }

    [Fact]
    public void Ordering_GroupsAlphabeticalUngroupedLast_StepsBeforeRest()
    {
        var groups = MacroNaming.GroupAndOrder(All);
        Assert.Equal(new[] { "ANY", "HW Z4", "PLZ", "Ungrouped" }, groups.Select(g => g.Group));
        Assert.Equal(new[] { Trade, Move }, groups[0].Macros.Select(m => m.Name));
        Assert.Equal(new[] { Booth, Trader }, groups[2].Macros.Select(m => m.Name));
    }

    [Fact]
    public void Render_ShapeAndCounts()
    {
        var text = MacroNaming.Render(All, null, null);
        Assert.StartsWith("6 macro(s) in 4 groups:", text);
        Assert.Contains("## PLZ (2)", text);
        Assert.Contains($"- {Booth} | id ", text);
        Assert.DoesNotContain("Hint:", text);
    }

    [Fact]
    public void Render_QueryAppearsInHeader()
        => Assert.StartsWith("1 macro(s) in 1 group, matching \"egg\":", MacroNaming.Render(All, "egg", null));

    [Fact]
    public void Render_NoMatch_ListsGroupCounts()
    {
        var text = MacroNaming.Render(All, "zebra", null);
        Assert.StartsWith("No macros matching \"zebra\"", text);
        Assert.Contains("- PLZ (2)", text);
        Assert.Contains("- Ungrouped (1)", text);
    }

    [Fact]
    public void Render_HintOnlyOverThresholdWithoutFilters()
    {
        var many = Enumerable.Range(0, 61).Select(i => M($"ANY | m{i}")).ToList();
        Assert.StartsWith("Hint: list_macros accepts query= and group=", MacroNaming.Render(many, null, null));
        Assert.DoesNotContain("Hint:", MacroNaming.Render(many, "m1", null));
    }
}
