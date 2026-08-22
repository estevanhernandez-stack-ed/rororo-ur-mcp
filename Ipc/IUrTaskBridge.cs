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
}

public sealed record BridgeMacro(string Id, string Name);

public sealed record BridgeRunResult(bool Ok, string? PlaybackId, string? Reason, string? Detail);

public sealed record BridgeStopResult(bool Ok, int Stopped, string? Reason, string? Detail);

/// <summary>Ur Task isn't reachable (pipe absent — not installed, or not running).</summary>
public sealed class BridgeUnavailableException : Exception
{
    public BridgeUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
