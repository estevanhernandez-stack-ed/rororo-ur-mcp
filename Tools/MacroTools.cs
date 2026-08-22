using System.ComponentModel;
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
}
