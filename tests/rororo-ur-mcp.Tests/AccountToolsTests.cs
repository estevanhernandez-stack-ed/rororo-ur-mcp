using Labs626.UrMcp.Ipc;
using Labs626.UrMcp.Tools;

namespace Labs626.UrMcp.Tests;

/// <summary>
/// The tool ↔ RPC mapping, asserted through the seam: names resolve before any command fires,
/// outcomes and failure reasons reach the text verbatim, and every failure mode is a readable
/// answer rather than an exception escaping to the MCP layer.
/// </summary>
public class AccountToolsTests
{
    private static FakeHost HostWith(params HostAccount[] accounts)
        => new() { Accounts = [.. accounts] };

    private static readonly HostAccount Pokey = new("id-pokey", 111, "Pokey", IsMain: true);
    private static readonly HostAccount Spud = new("id-spud", 222, "Spud", IsMain: false);

    [Fact]
    public async Task ListAccounts_ShowsMainRunningStateAndGame()
    {
        var host = HostWith(Pokey, Spud);
        host.Running = [new("id-pokey", 111, "Pokey", 4242, 8737899170, "Pet Simulator 99!")];

        var text = await AccountTools.ListAccounts(host);

        Assert.Contains("Pokey [main]", text);
        Assert.Contains("in Pet Simulator 99!", text);
        Assert.Contains("Spud", text);
        Assert.DoesNotContain("Spud [main]", text);
    }

    [Fact]
    public async Task LaunchAccount_ResolvesTheNameToTheId()
    {
        var host = HostWith(Pokey, Spud);

        var text = await AccountTools.LaunchAccount(host, "spud");

        Assert.Contains(("launch", "id-spud"), host.Calls.Select(c => (c.Kind, c.AccountId)));
        Assert.Contains("Launch requested for Spud", text);
    }

    [Fact]
    public async Task LaunchAccount_DeclinedReasonReachesTheText()
    {
        var host = HostWith(Pokey);
        host.LaunchResult = new(false, "cookie expired");

        var text = await AccountTools.LaunchAccount(host, "Pokey");

        Assert.Contains("cookie expired", text);
    }

    [Fact]
    public async Task FollowMain_ResolvesTheMainsUid()
    {
        var host = HostWith(Pokey, Spud);

        var text = await AccountTools.FollowMain(host, "Spud");

        var call = Assert.Single(host.Calls, c => c.Kind == "launch-follow");
        Assert.Equal("id-spud", call.AccountId);
        Assert.Equal(111L, call.Arg);
        Assert.Contains("Spud is following Pokey", text);
    }

    [Fact]
    public async Task FollowMain_TheMainItself_GetsARedirect()
    {
        var host = HostWith(Pokey, Spud);
        var text = await AccountTools.FollowMain(host, "Pokey");
        Assert.Contains("IS the main", text);
        Assert.DoesNotContain(host.Calls, c => c.Kind == "launch-follow");
    }

    [Fact]
    public async Task UnknownAccount_ListsTheCandidates()
    {
        var host = HostWith(Pokey, Spud);
        var text = await AccountTools.LaunchAccount(host, "Koii");
        Assert.Contains("No account named 'Koii'", text);
        Assert.Contains("Pokey", text);
    }

    [Fact]
    public async Task HostDown_IsAReadableAnswer()
    {
        var host = new FakeHost { Throw = new HostUnavailableException("RoRoRo isn't running — open it and try again.") };
        var text = await AccountTools.ListAccounts(host);
        Assert.Contains("RoRoRo isn't running", text);
    }

    [Fact]
    public async Task ConsentDenied_IsAReadableAnswer()
    {
        var host = new FakeHost { Throw = new ConsentDeniedException("Plugin '626labs.ur-mcp' has not been granted 'host.queries.accounts'. Grant it in RoRoRo's Plugins window, then try again.") };
        var text = await AccountTools.ListAccounts(host);
        Assert.Contains("Plugins window", text);
    }

    [Fact]
    public async Task StopAccounts_ResolvesNames_AndReportsAsyncSemantics()
    {
        var host = HostWith(Pokey, Spud);
        host.StopResult = new(2, []);

        var text = await AccountTools.StopAccounts(host, ["Pokey", "Spud"]);

        var call = Assert.Single(host.Calls, c => c.Kind == "stop");
        Assert.Equal(new[] { "id-pokey", "id-spud" }, (IReadOnlyList<string>)call.Arg!);
        Assert.Contains("2 client(s)", text);
        Assert.Contains("asynchronous", text); // stopped means ISSUED — the proto's own caveat
    }

    [Fact]
    public async Task RunningStatus_SurvivesAMissingCurrentServerGrant()
    {
        var host = HostWith(Pokey);
        host.Running = [new("id-pokey", 111, "Pokey", 4242, 0, "")];
        // Simulate the current-server capability being the ONLY missing grant: the fake throws
        // per-call, so use a host that answers running but denies current-server.
        var selective = new SelectiveHost(host);

        var text = await AccountTools.RunningStatus(selective);

        Assert.Contains("game unknown", text);
        Assert.Contains("current-server not granted", text);
    }

    private sealed class SelectiveHost(FakeHost inner) : IRoRoRoHost
    {
        public Task<HostInfoDto> GetHostInfoAsync(CancellationToken ct = default) => inner.GetHostInfoAsync(ct);
        public Task<IReadOnlyList<HostAccount>> GetAccountsAsync(CancellationToken ct = default) => inner.GetAccountsAsync(ct);
        public Task<IReadOnlyList<RunningAccountDto>> GetRunningAccountsAsync(CancellationToken ct = default) => inner.GetRunningAccountsAsync(ct);
        public Task<CurrentServerInfo> GetCurrentServerAsync(CancellationToken ct = default)
            => throw new ConsentDeniedException("not granted");
        public Task<IReadOnlyList<AccountActivityDto>> GetAccountActivityAsync(CancellationToken ct = default) => inner.GetAccountActivityAsync(ct);
        public Task<LaunchOutcome> RequestLaunchAsync(string accountId, CancellationToken ct = default) => inner.RequestLaunchAsync(accountId, ct);
        public Task<LaunchOutcome> LaunchTargetShareAsync(string accountId, string shareUrl, CancellationToken ct = default) => inner.LaunchTargetShareAsync(accountId, shareUrl, ct);
        public Task<LaunchOutcome> LaunchTargetFollowAsync(string accountId, long followUserId, CancellationToken ct = default) => inner.LaunchTargetFollowAsync(accountId, followUserId, ct);
        public Task<StopOutcome> StopAccountsAsync(IReadOnlyList<string> accountIds, CancellationToken ct = default) => inner.StopAccountsAsync(accountIds, ct);
    }
}
