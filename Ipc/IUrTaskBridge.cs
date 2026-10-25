namespace Labs626.UrMcp.Ipc;

/// <summary>
/// The connector's view of Ur Task's action bridge (the `626labs-ur-task` pipe) — the seam the
/// macro tools are unit-tested against. <see cref="UrTaskBridgeClient"/> is the real
/// length-prefixed-JSON implementation.
/// </summary>
public interface IUrTaskBridge
{
    Task<IReadOnlyList<BridgeMacro>> ListMacrosAsync(CancellationToken ct = default);

    /// <param name="targets">Decimal Roblox user-id strings, or the literal ["foreground"].</param>
    Task<BridgeRunResult> RunMacroAsync(string macroId, IReadOnlyList<string> targets, bool repeat, CancellationToken ct = default);

    Task<BridgeStopResult> StopMacroAsync(string? playbackId, CancellationToken ct = default);

    /// <summary>How a playback is doing, or how it ended. Ur Task keeps finished playbacks for 10
    /// minutes; an unknown or expired id is an ok=false refusal. Ur Task 0.8.0 has no such method
    /// and refuses it as an unknown method.</summary>
    Task<BridgePlaybackResult> GetPlaybackAsync(string playbackId, CancellationToken ct = default);

    /// <summary>Keep-alive state, every tracked account's idle time and deadline, and paused
    /// playbacks (Ur Task 0.13.0+). Older Ur Task refuses it as an unknown method.</summary>
    Task<BridgeStatus> GetStatusAsync(CancellationToken ct = default);
}

public sealed record BridgeMacro(string Id, string Name);

public sealed record BridgeRunResult(bool Ok, string? PlaybackId, string? Reason, string? Detail);

public sealed record BridgeStopResult(bool Ok, int Stopped, string? Reason, string? Detail);

/// <param name="State">running | finished | stopped | failed. stopped = a user or StopMacro ended
/// it; failed = a check stopped it, with Reason/Detail as readable sentences.</param>
public sealed record BridgePlaybackResult(bool Ok, string? State, string? Reason, string? Detail, int? StepIndex);

/// <param name="State">running | stopped | breakglass (break-glass wins).</param>
public sealed record BridgeKeepAlive(string State, bool Turbo);

/// <param name="Role">active | keep-alive | off.</param>
/// <param name="LastInputAt">Local ISO 8601 with offset; null when the last input is unknown.</param>
/// <param name="DeadlineAt">Local ISO 8601 with offset; when this account needs input by.</param>
public sealed record BridgeAccountStatus(
    string UserId, string Name, string Role, string? LastInputAt, string? LastInputSource,
    int? IdleSeconds, string? DeadlineAt, int LimitMinutes, int Misses, string? LastMiss, string State);

/// <param name="PausedFor">The account keep-alive is serving while the playback waits.</param>
public sealed record BridgePlaybackStatus(string PlaybackId, bool Paused, string? PausedFor);

public sealed record BridgeStatus(
    bool Ok, BridgeKeepAlive? KeepAlive, IReadOnlyList<BridgeAccountStatus>? Accounts,
    IReadOnlyList<BridgePlaybackStatus>? Playbacks, string? Reason, string? Detail);

/// <summary>Ur Task isn't reachable (pipe absent — not installed, or not running).</summary>
public sealed class BridgeUnavailableException : Exception
{
    public BridgeUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
