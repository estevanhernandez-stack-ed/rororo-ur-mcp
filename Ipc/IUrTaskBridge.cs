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
}

public sealed record BridgeMacro(string Id, string Name);

public sealed record BridgeRunResult(bool Ok, string? PlaybackId, string? Reason, string? Detail);

public sealed record BridgeStopResult(bool Ok, int Stopped, string? Reason, string? Detail);

/// <param name="State">running | finished | stopped | failed. stopped = a user or StopMacro ended
/// it; failed = a check stopped it, with Reason/Detail as readable sentences.</param>
public sealed record BridgePlaybackResult(bool Ok, string? State, string? Reason, string? Detail, int? StepIndex);

/// <summary>Ur Task isn't reachable (pipe absent — not installed, or not running).</summary>
public sealed class BridgeUnavailableException : Exception
{
    public BridgeUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
