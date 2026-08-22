using Labs626.UrMcp.Ipc;
using Labs626.UrMcp.Resolution;

namespace Labs626.UrMcp.Tests;

public class NameResolverTests
{
    private static readonly List<HostAccount> Accounts =
    [
        new("id-pokey", 111, "Pokey", IsMain: true),
        new("id-spud", 222, "Spud", IsMain: false),
        new("id-clover", 0, "Clover", IsMain: false),
    ];

    [Fact]
    public void ResolvesByDisplayName_CaseInsensitive()
        => Assert.Equal("id-pokey", NameResolver.ResolveAccount(Accounts, "pokey").AccountId);

    [Fact]
    public void ResolvesByExactId()
        => Assert.Equal("Spud", NameResolver.ResolveAccount(Accounts, "ID-SPUD").DisplayName);

    [Fact]
    public void UnknownName_ThrowsListingKnownAccounts()
    {
        var ex = Assert.Throws<ResolutionException>(() => NameResolver.ResolveAccount(Accounts, "Koii"));
        Assert.Contains("No account named 'Koii'", ex.Message);
        Assert.Contains("Pokey", ex.Message);
        Assert.Contains("Clover", ex.Message);
    }

    [Fact]
    public void AmbiguousName_ThrowsListingIds()
    {
        var dupes = new List<HostAccount>
        {
            new("id-1", 1, "Twin", false),
            new("id-2", 2, "Twin", false),
        };
        var ex = Assert.Throws<ResolutionException>(() => NameResolver.ResolveAccount(dupes, "Twin"));
        Assert.Contains("ambiguous", ex.Message);
        Assert.Contains("id-1", ex.Message);
        Assert.Contains("id-2", ex.Message);
    }

    [Fact]
    public void ResolveMain_FindsTheMain()
        => Assert.Equal("Pokey", NameResolver.ResolveMain(Accounts).DisplayName);

    [Fact]
    public void ResolveMain_NoMain_ThrowsWithTheFix()
    {
        var noMain = Accounts.Select(a => a with { IsMain = false }).ToList();
        var ex = Assert.Throws<ResolutionException>(() => NameResolver.ResolveMain(noMain));
        Assert.Contains("No account is marked as main", ex.Message);
    }

    [Fact]
    public void ResolveMacro_ByNameAndId_WithCandidatesOnMiss()
    {
        var macros = new List<BridgeMacro> { new("m-1", "Farm"), new("m-2", "Get in position") };
        Assert.Equal("m-1", NameResolver.ResolveMacro(macros, "farm").Id);
        Assert.Equal("Farm", NameResolver.ResolveMacro(macros, "M-1").Name);
        var ex = Assert.Throws<ResolutionException>(() => NameResolver.ResolveMacro(macros, "Nope"));
        Assert.Contains("Get in position", ex.Message);
    }
}
