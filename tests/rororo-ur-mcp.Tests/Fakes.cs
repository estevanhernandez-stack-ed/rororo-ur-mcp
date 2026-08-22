using Labs626.UrMcp.Ipc;

namespace Labs626.UrMcp.Tests;

/// <summary>
/// Seedable fakes for the two IPC seams. Set the list/result properties to script answers;
/// set one of the throw properties to script a failure mode (host down, consent missing).
/// </summary>
public sealed class FakeHost : IRoRoRoHost
{
    public HostInfoDto Info { get; set; } = new("1.22.0", true, "On");
    public List<HostAccount> Accounts { get; set; } = [];
    public List<RunningAccountDto> Running { get; set; } = [];
    public CurrentServerInfo Server { get; set; } = new(false, "");
    public List<AccountActivityDto> Activity { get; set; } = [];
    public LaunchOutcome LaunchResult { get; set; } = new(true, null);
    public StopOutcome StopResult { get; set; } = new(0, []);

    public Exception? Throw { get; set; }

    public List<(string Kind, string AccountId, object? Arg)> Calls { get; } = [];

    private T Answer<T>(T value, string kind, string accountId = "", object? arg = null)
    {
        if (Throw is not null) throw Throw;
        Calls.Add((kind, accountId, arg));
        return value;
    }

    public Task<HostInfoDto> GetHostInfoAsync(CancellationToken ct = default)
        => Task.FromResult(Answer(Info, "host-info"));
    public Task<IReadOnlyList<HostAccount>> GetAccountsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<HostAccount>>(Answer(Accounts, "get-accounts"));
    public Task<IReadOnlyList<RunningAccountDto>> GetRunningAccountsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RunningAccountDto>>(Answer(Running, "get-running"));
    public Task<CurrentServerInfo> GetCurrentServerAsync(CancellationToken ct = default)
        => Task.FromResult(Answer(Server, "current-server"));
    public Task<IReadOnlyList<AccountActivityDto>> GetAccountActivityAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AccountActivityDto>>(Answer(Activity, "activity"));
    public Task<LaunchOutcome> RequestLaunchAsync(string accountId, CancellationToken ct = default)
        => Task.FromResult(Answer(LaunchResult, "launch", accountId));
    public Task<LaunchOutcome> LaunchTargetShareAsync(string accountId, string shareUrl, CancellationToken ct = default)
        => Task.FromResult(Answer(LaunchResult, "launch-share", accountId, shareUrl));
    public Task<LaunchOutcome> LaunchTargetFollowAsync(string accountId, long followUserId, CancellationToken ct = default)
        => Task.FromResult(Answer(LaunchResult, "launch-follow", accountId, followUserId));
    public Task<StopOutcome> StopAccountsAsync(IReadOnlyList<string> accountIds, CancellationToken ct = default)
        => Task.FromResult(Answer(StopResult, "stop", "", accountIds));
}

public sealed class FakeBridge : IUrTaskBridge
{
    public List<BridgeMacro> Macros { get; set; } = [];
    public BridgeRunResult RunResult { get; set; } = new(true, "pb-1", null, null);
    public BridgeStopResult StopResult { get; set; } = new(true, 0, null, null);

    public Exception? Throw { get; set; }

    public (string MacroId, IReadOnlyList<string> Targets, bool Repeat)? LastRun { get; private set; }
    public string? LastStopId { get; private set; }
    public bool StopCalled { get; private set; }

    public Task<IReadOnlyList<BridgeMacro>> ListMacrosAsync(CancellationToken ct = default)
        => Throw is not null ? throw Throw : Task.FromResult<IReadOnlyList<BridgeMacro>>(Macros);

    public Task<BridgeRunResult> RunMacroAsync(string macroId, IReadOnlyList<string> targets, bool repeat, CancellationToken ct = default)
    {
        if (Throw is not null) throw Throw;
        LastRun = (macroId, targets, repeat);
        return Task.FromResult(RunResult);
    }

    public Task<BridgeStopResult> StopMacroAsync(string? playbackId, CancellationToken ct = default)
    {
        if (Throw is not null) throw Throw;
        StopCalled = true;
        LastStopId = playbackId;
        return Task.FromResult(StopResult);
    }
}
