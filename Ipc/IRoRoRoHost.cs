namespace Labs626.UrMcp.Ipc;

/// <summary>
/// The connector's view of the RoRoRo plugin host, as plain DTOs — the seam the account tools are
/// unit-tested against. <see cref="RoRoRoHostClient"/> is the real gRPC implementation.
/// </summary>
public interface IRoRoRoHost
{
    Task<HostInfoDto> GetHostInfoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<HostAccount>> GetAccountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RunningAccountDto>> GetRunningAccountsAsync(CancellationToken ct = default);
    Task<CurrentServerInfo> GetCurrentServerAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccountActivityDto>> GetAccountActivityAsync(CancellationToken ct = default);
    Task<LaunchOutcome> RequestLaunchAsync(string accountId, CancellationToken ct = default);
    Task<LaunchOutcome> LaunchTargetShareAsync(string accountId, string shareUrl, CancellationToken ct = default);
    Task<LaunchOutcome> LaunchTargetFollowAsync(string accountId, long followUserId, CancellationToken ct = default);
    Task<StopOutcome> StopAccountsAsync(IReadOnlyList<string> accountIds, CancellationToken ct = default);
}

public sealed record HostInfoDto(string Version, bool MultiInstanceEnabled, string MultiInstanceState);

public sealed record HostAccount(string AccountId, long RobloxUserId, string DisplayName, bool IsMain);

public sealed record RunningAccountDto(
    string AccountId, long RobloxUserId, string DisplayName, int ProcessId, long PlaceId, string PlaceName);

public sealed record CurrentServerInfo(bool HasServer, string ShareUrl);

public sealed record AccountActivityDto(string AccountId, long LastActivityUnixMs, long SecondsSinceActivity);

public sealed record LaunchOutcome(bool Accepted, string? FailureReason);

public sealed record StopOutcome(int StoppedCount, IReadOnlyList<string> FailedAccountIds);

/// <summary>RoRoRo isn't reachable (pipe absent, connection refused, host errored).</summary>
public sealed class HostUnavailableException : Exception
{
    public HostUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The host refused a call because the user hasn't granted the capability it needs.</summary>
public sealed class ConsentDeniedException : Exception
{
    public ConsentDeniedException(string message) : base(message) { }
}
