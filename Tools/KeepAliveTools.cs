using System.ComponentModel;
using System.Globalization;
using System.Text;
using Labs626.UrMcp.Ipc;
using ModelContextProtocol.Server;

namespace Labs626.UrMcp.Tools;

/// <summary>
/// Read-only view of Ur Task's keep-alive, so an agent can see which accounts need input and
/// when before it commits to a long macro run.
/// </summary>
[McpServerToolType]
public static class KeepAliveTools
{
    [McpServerTool(Name = "keepalive_status"), Description(
        "Keep-alive state in Ur Task: whether it is running, and for each account its idle time, the time by which it needs input (its deadline), misses, and whether keep-alive is off for it. Use it before a long macro run to see which accounts need a Space and when.")]
    public static async Task<string> KeepAliveStatus(IUrTaskBridge bridge)
        => await AccountTools.Guard(async () =>
        {
            var status = await bridge.GetStatusAsync().ConfigureAwait(false);
            if (!status.Ok)
                return status.Detail?.StartsWith("Unknown method", StringComparison.OrdinalIgnoreCase) == true
                    ? "Update Ur Task to 0.13.0 or later to see keep-alive status."
                    : $"Ur Task refused: {status.Reason} — {status.Detail}";

            var sb = new StringBuilder();
            sb.AppendLine(Headline(status.KeepAlive));
            foreach (var p in status.Playbacks ?? [])
                if (p.Paused) sb.AppendLine($"Macro {p.PlaybackId} is paused for {p.PausedFor}.");
            foreach (var a in status.Accounts ?? [])
                sb.AppendLine(AccountLine(a));
            return sb.ToString().TrimEnd();
        });

    private static string Headline(BridgeKeepAlive? ka)
    {
        var state = ka?.State?.ToLowerInvariant();
        return state switch
        {
            "running" => $"Keep-alive is running (turbo {(ka!.Turbo ? "on" : "off")}).",
            "breakglass" => "Keep-alive is paused: break-glass is on.",
            _ => "Keep-alive is stopped.",
        };
    }

    private static string AccountLine(BridgeAccountStatus a)
    {
        var idle = a.IdleSeconds is { } s ? $"idle {s / 60}m" : "idle unknown";
        var line = string.Equals(a.Role, "off", StringComparison.OrdinalIgnoreCase)
            ? $"{a.Name}: keep-alive off, {idle}"
            : $"{a.Name}: {idle}, needs input by {Deadline(a.DeadlineAt)} ({a.Role}), misses {a.Misses}";
        return string.IsNullOrEmpty(a.LastMiss) ? line : $"{line} — last miss: {a.LastMiss}";
    }

    private static string Deadline(string? iso)
        => DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
            : "an unknown time";
}
