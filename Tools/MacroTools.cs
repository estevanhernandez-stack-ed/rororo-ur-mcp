using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Labs626.UrMcp.Ipc;
using Labs626.UrMcp.Resolution;
using ModelContextProtocol.Server;

namespace Labs626.UrMcp.Tools;

/// <summary>
/// The macro half of the tool surface. These tools only ever ASK Ur Task — the input-synthesis
/// consent lives in Ur Task's own capability grants, and this connector adds no synthesis path
/// of its own. Targets resolve through RoRoRo's saved accounts to the decimal Roblox user-id
/// strings the bridge wants, or the literal "foreground".
/// </summary>
[McpServerToolType]
public static class MacroTools
{
    [McpServerTool(Name = "list_macros"), Description(
        "List the macros recorded in Ur Task (id + name), for use with run_macro.")]
    public static async Task<string> ListMacros(IUrTaskBridge bridge)
        => await AccountTools.Guard(async () =>
        {
            var macros = await bridge.ListMacrosAsync().ConfigureAwait(false);
            if (macros.Count == 0) return "Ur Task has no recorded macros yet.";

            var sb = new StringBuilder();
            sb.AppendLine($"{macros.Count} macro(s):");
            foreach (var m in macros) sb.AppendLine($"- {m.Name} | id {m.Id}");
            return sb.ToString().TrimEnd();
        });

    [McpServerTool(Name = "run_macro"), Description(
        "Run an Ur Task macro on one or more accounts (or the foreground window). Set repeat=true to loop it until stop_macro. The bridge refuses while another sequence is running — stop it first.")]
    public static async Task<string> RunMacro(IRoRoRoHost host, IUrTaskBridge bridge,
        [Description("Macro name or id, e.g. \"Farm\"")] string macro,
        [Description("Account display names/ids to run on, or [\"foreground\"] for the focused window. Empty = foreground.")] string[]? targets = null,
        [Description("Loop the macro until stopped")] bool repeat = false)
        => await AccountTools.Guard(async () =>
        {
            var resolvedMacro = NameResolver.ResolveMacro(await bridge.ListMacrosAsync().ConfigureAwait(false), macro);

            IReadOnlyList<string> wireTargets;
            string targetsShown;
            if (targets is null || targets.Length == 0
                || (targets.Length == 1 && string.Equals(targets[0], "foreground", StringComparison.OrdinalIgnoreCase)))
            {
                wireTargets = ["foreground"];
                targetsShown = "the foreground window";
            }
            else
            {
                var saved = await host.GetAccountsAsync().ConfigureAwait(false);
                var resolved = targets.Select(t => NameResolver.ResolveAccount(saved, t)).ToList();
                var unresolvedUid = resolved.FirstOrDefault(r => r.RobloxUserId <= 0);
                if (unresolvedUid is not null)
                    return $"{unresolvedUid.DisplayName} has no resolved Roblox user id yet — launch it once first, then retry.";
                wireTargets = resolved.Select(r => r.RobloxUserId.ToString()).ToList();
                targetsShown = string.Join(", ", resolved.Select(r => r.DisplayName));
            }

            var result = await bridge.RunMacroAsync(resolvedMacro.Id, wireTargets, repeat).ConfigureAwait(false);
            return result.Ok
                ? $"Running '{resolvedMacro.Name}' on {targetsShown}{(repeat ? " on repeat" : "")}. Playback id: {result.PlaybackId}."
                : $"Ur Task refused: {result.Reason} — {result.Detail}";
        });

    [McpServerTool(Name = "stop_macro"), Description(
        "Stop a running macro playback — by the playback id run_macro returned, or every active playback when no id is given.")]
    public static async Task<string> StopMacro(IUrTaskBridge bridge,
        [Description("The playback id to stop; omit to stop everything")] string? playbackId = null)
        => await AccountTools.Guard(async () =>
        {
            var result = await bridge.StopMacroAsync(playbackId).ConfigureAwait(false);
            if (!result.Ok) return $"Ur Task refused the stop: {result.Reason} — {result.Detail}";
            return result.Stopped == 0
                ? "Nothing was running."
                : $"Stopped {result.Stopped} playback(s).";
        });

    [McpServerTool(Name = "wait_for_macro"), Description(
        "Wait for a macro playback to end, up to a timeout, and report how it ended: finished, stopped, or failed with the reason (e.g. a colour check that didn't match). Takes the playback id run_macro returned. timeoutSeconds=0 checks once without waiting. Ur Task keeps ended playbacks for 10 minutes.")]
    public static async Task<string> WaitForMacro(IUrTaskBridge bridge,
        [Description("The playback id run_macro returned")] string playbackId,
        [Description("Seconds to wait before giving up (default 60, max 120; 0 = check once)")] int timeoutSeconds = 60)
        => await AccountTools.Guard(async () =>
        {
            var deadline = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 0, 120));
            var sw = Stopwatch.StartNew();

            while (true)
            {
                var result = await bridge.GetPlaybackAsync(playbackId).ConfigureAwait(false);
                if (!result.Ok)
                    return IsUnknownMethod(result)
                        ? "This Ur Task is too old to report how a macro ended — update Ur Task to use wait_for_macro."
                        : $"Ur Task refused: {result.Reason} — {result.Detail}";

                if (!string.Equals(result.State, "running", StringComparison.OrdinalIgnoreCase))
                    return DescribeEnded(playbackId, result);

                if (sw.Elapsed >= deadline)
                {
                    var step = result.StepIndex is { } i ? $", at step {i}" : "";
                    return deadline == TimeSpan.Zero
                        ? $"Playback {playbackId} is still running{step}."
                        : $"Timed out after {deadline.TotalSeconds:F0}s: playback {playbackId} is still running{step}. A repeat playback runs until stop_macro.";
                }

                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        });

    // Ur Task 0.8.0 answers a method it doesn't know with reason "refused" and this detail.
    private static bool IsUnknownMethod(BridgePlaybackResult r)
        => r.Detail?.StartsWith("Unknown method", StringComparison.OrdinalIgnoreCase) == true;

    private static string DescribeEnded(string playbackId, BridgePlaybackResult r)
    {
        var why = (r.Reason, r.Detail) switch
        {
            (null or "", null or "") => "",
            (null or "", var d) => $": {d}",
            (var reason, null or "") => $": {reason}",
            var (reason, d) => $": {reason} — {d}",
        };
        var text = r.State?.ToLowerInvariant() switch
        {
            "finished" => $"Playback {playbackId} finished",
            "stopped" => $"Playback {playbackId} was stopped{why}",
            "failed" => $"Playback {playbackId} failed{why}",
            _ => $"Playback {playbackId} ended in state '{r.State}'{why}",
        };
        // Ur Task's reason sentences usually end in a period already.
        return text.EndsWith('.') ? text : text + ".";
    }
}
