using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Labs626.UrMcp.Ipc;
using Labs626.UrMcp.Resolution;
using ModelContextProtocol.Server;

namespace Labs626.UrMcp.Tools;

/// <summary>
/// The account half of the tool surface — every tool maps to a consent-gated host RPC and every
/// failure becomes readable tool-result TEXT, never a protocol error: "RoRoRo isn't running",
/// "consent not granted", and unknown-name messages listing candidates are answers the operator
/// can act on, and an MCP exception is not.
/// </summary>
[McpServerToolType]
public static class AccountTools
{
    [McpServerTool(Name = "list_accounts"), Description(
        "List every account saved in RoRoRo: display name, Roblox user id, which one is the main, and whether it is currently running (with the game it is in).")]
    public static async Task<string> ListAccounts(IRoRoRoHost host)
        => await Guard(async () =>
        {
            var saved = await host.GetAccountsAsync().ConfigureAwait(false);
            if (saved.Count == 0) return "No accounts are saved in RoRoRo yet.";

            var running = (await host.GetRunningAccountsAsync().ConfigureAwait(false))
                .ToDictionary(r => r.AccountId, r => r);

            var sb = new StringBuilder();
            sb.AppendLine($"{saved.Count} saved account(s):");
            foreach (var a in saved)
            {
                sb.Append("- ").Append(a.DisplayName);
                if (a.IsMain) sb.Append(" [main]");
                sb.Append(a.RobloxUserId > 0 ? $" (uid {a.RobloxUserId})" : " (uid unresolved)");
                if (running.TryGetValue(a.AccountId, out var r))
                {
                    sb.Append(r.PlaceId > 0 ? $" — running, in {r.PlaceName}" : " — running");
                }
                sb.Append($" | id {a.AccountId}");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        });

    [McpServerTool(Name = "launch_account"), Description(
        "Launch a saved account into Roblox (its per-row game pick, or the Roblox home). Accepts the account's display name or id.")]
    public static async Task<string> LaunchAccount(IRoRoRoHost host,
        [Description("Account display name or id, e.g. \"Pokey\"")] string account)
        => await Guard(async () =>
        {
            var target = NameResolver.ResolveAccount(await host.GetAccountsAsync().ConfigureAwait(false), account);
            var outcome = await host.RequestLaunchAsync(target.AccountId).ConfigureAwait(false);
            return outcome.Accepted
                ? $"Launch requested for {target.DisplayName}. Use running_status or wait_for_ingame to confirm it lands."
                : $"RoRoRo declined the launch for {target.DisplayName}: {outcome.FailureReason}";
        });

    [McpServerTool(Name = "launch_into_game"), Description(
        "Launch a saved account into a specific game or server. The game accepts any link RoRoRo understands: a private-server share URL, a roblox.com game URL, or a bare place id.")]
    public static async Task<string> LaunchIntoGame(IRoRoRoHost host,
        [Description("Account display name or id")] string account,
        [Description("Share URL, game URL, or bare place id")] string game)
        => await Guard(async () =>
        {
            var target = NameResolver.ResolveAccount(await host.GetAccountsAsync().ConfigureAwait(false), account);
            var outcome = await host.LaunchTargetShareAsync(target.AccountId, game).ConfigureAwait(false);
            return outcome.Accepted
                ? $"Launch into that game requested for {target.DisplayName}."
                : $"RoRoRo declined the launch for {target.DisplayName}: {outcome.FailureReason}";
        });

    [McpServerTool(Name = "follow_main"), Description(
        "Launch a saved account to follow the MAIN account into whatever server the main is in. Resolves the main automatically.")]
    public static async Task<string> FollowMain(IRoRoRoHost host,
        [Description("The account that should follow, by display name or id")] string account)
        => await Guard(async () =>
        {
            var accounts = await host.GetAccountsAsync().ConfigureAwait(false);
            var follower = NameResolver.ResolveAccount(accounts, account);
            var main = NameResolver.ResolveMain(accounts);
            if (main.RobloxUserId <= 0)
                return $"The main ({main.DisplayName}) has no resolved Roblox user id yet — launch it once first.";
            if (string.Equals(follower.AccountId, main.AccountId, StringComparison.OrdinalIgnoreCase))
                return $"{follower.DisplayName} IS the main — pick a different follower.";

            var outcome = await host.LaunchTargetFollowAsync(follower.AccountId, main.RobloxUserId).ConfigureAwait(false);
            return outcome.Accepted
                ? $"{follower.DisplayName} is following {main.DisplayName} in."
                : $"RoRoRo declined the follow for {follower.DisplayName}: {outcome.FailureReason}";
        });

    [McpServerTool(Name = "follow_friend"), Description(
        "Launch a saved account to follow a specific Roblox user (by user id) into their server. Roblox's own permissions apply — a privacy-hidden target lands at home, and the failure reason will say so.")]
    public static async Task<string> FollowFriend(IRoRoRoHost host,
        [Description("The account that should follow, by display name or id")] string account,
        [Description("The Roblox user id to follow")] long friendUserId)
        => await Guard(async () =>
        {
            var follower = NameResolver.ResolveAccount(await host.GetAccountsAsync().ConfigureAwait(false), account);
            var outcome = await host.LaunchTargetFollowAsync(follower.AccountId, friendUserId).ConfigureAwait(false);
            return outcome.Accepted
                ? $"{follower.DisplayName} is following user {friendUserId} in."
                : $"RoRoRo declined the follow for {follower.DisplayName}: {outcome.FailureReason}";
        });

    [McpServerTool(Name = "running_status"), Description(
        "Which accounts are running right now, which game each is in, and the most recently launched private server if any.")]
    public static async Task<string> RunningStatus(IRoRoRoHost host)
        => await Guard(async () =>
        {
            var running = await host.GetRunningAccountsAsync().ConfigureAwait(false);
            var sb = new StringBuilder();
            if (running.Count == 0)
            {
                sb.AppendLine("No accounts are running.");
            }
            else
            {
                sb.AppendLine($"{running.Count} running:");
                foreach (var r in running)
                {
                    sb.Append("- ").Append(r.DisplayName)
                      .Append(r.PlaceId > 0 ? $" — in {r.PlaceName} (place {r.PlaceId})" : " — game unknown (presence may lag a fresh launch)")
                      .Append($", pid {r.ProcessId}")
                      .AppendLine();
                }
            }

            try
            {
                var server = await host.GetCurrentServerAsync().ConfigureAwait(false);
                if (server.HasServer) sb.Append("Current private server: ").AppendLine(server.ShareUrl);
            }
            catch (ConsentDeniedException)
            {
                // current-server is a separate capability; a status readout should not fail
                // outright because one optional grant is missing.
                sb.AppendLine("(Private-server link unavailable — host.queries.current-server not granted.)");
            }

            return sb.ToString().TrimEnd();
        });

    [McpServerTool(Name = "wait_for_ingame"), Description(
        "Wait until an account shows as in a game (presence confirmed), up to a timeout. Use after a launch to confirm it landed. Polls RoRoRo's presence; returns early on success.")]
    public static async Task<string> WaitForInGame(IRoRoRoHost host,
        [Description("Account display name or id")] string account,
        [Description("Seconds to wait before giving up (default 60, max 120)")] int timeoutSeconds = 60)
        => await Guard(async () =>
        {
            var target = NameResolver.ResolveAccount(await host.GetAccountsAsync().ConfigureAwait(false), account);
            var deadline = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 120));
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < deadline)
            {
                var running = await host.GetRunningAccountsAsync().ConfigureAwait(false);
                var hit = running.FirstOrDefault(r =>
                    string.Equals(r.AccountId, target.AccountId, StringComparison.OrdinalIgnoreCase));
                if (hit is { PlaceId: > 0 })
                    return $"{target.DisplayName} is in {hit.PlaceName} (place {hit.PlaceId}) after {sw.Elapsed.TotalSeconds:F0}s.";

                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }

            var final = (await host.GetRunningAccountsAsync().ConfigureAwait(false))
                .FirstOrDefault(r => string.Equals(r.AccountId, target.AccountId, StringComparison.OrdinalIgnoreCase));
            return final is null
                ? $"Timed out after {deadline.TotalSeconds:F0}s: {target.DisplayName} never showed as running. The launch may have failed — check running_status."
                : $"Timed out after {deadline.TotalSeconds:F0}s: {target.DisplayName} is running (pid {final.ProcessId}) but presence hasn't confirmed a game yet.";
        });

    [McpServerTool(Name = "stop_accounts"), Description(
        "Close the Roblox clients of specific accounts (name or id), or every tracked client when none are named. Graceful close, then kill after a grace. Unsaved in-game progress in those clients is lost. Stopping is issued asynchronously — verify with running_status.")]
    public static async Task<string> StopAccounts(IRoRoRoHost host,
        [Description("Accounts to stop, by display name or id. Empty = every tracked client.")] string[]? accounts = null)
        => await Guard(async () =>
        {
            var ids = new List<string>();
            var named = "every tracked client";
            if (accounts is { Length: > 0 })
            {
                var saved = await host.GetAccountsAsync().ConfigureAwait(false);
                var resolved = accounts.Select(a => NameResolver.ResolveAccount(saved, a)).ToList();
                ids.AddRange(resolved.Select(r => r.AccountId));
                named = string.Join(", ", resolved.Select(r => r.DisplayName));
            }

            var outcome = await host.StopAccountsAsync(ids).ConfigureAwait(false);
            var sb = new StringBuilder();
            sb.Append($"Stop issued for {named}: {outcome.StoppedCount} client(s). ");
            sb.Append("Stopping is asynchronous — confirm with running_status.");
            if (outcome.FailedAccountIds.Count > 0)
                sb.Append($" Not stopped (unknown or untracked): {string.Join(", ", outcome.FailedAccountIds)}.");
            return sb.ToString();
        });

    [McpServerTool(Name = "account_activity"), Description(
        "How long each account has been idle (no input seen), as timestamps — useful for spotting an alt that fell out of its loop.")]
    public static async Task<string> AccountActivity(IRoRoRoHost host)
        => await Guard(async () =>
        {
            var items = await host.GetAccountActivityAsync().ConfigureAwait(false);
            if (items.Count == 0) return "No activity data — no accounts are being tracked right now.";

            var names = (await host.GetAccountsAsync().ConfigureAwait(false))
                .ToDictionary(a => a.AccountId, a => a.DisplayName);

            var sb = new StringBuilder();
            foreach (var i in items)
            {
                var name = names.TryGetValue(i.AccountId, out var n) ? n : i.AccountId;
                sb.AppendLine($"- {name}: idle {TimeSpan.FromSeconds(i.SecondsSinceActivity):hh\\:mm\\:ss}");
            }
            return sb.ToString().TrimEnd();
        });

    [McpServerTool(Name = "host_info"), Description(
        "RoRoRo's version and multi-instance (mutex) state — the first thing to check in a smoke run or when launches misbehave.")]
    public static async Task<string> HostInfo(IRoRoRoHost host)
        => await Guard(async () =>
        {
            var info = await host.GetHostInfoAsync().ConfigureAwait(false);
            return $"RoRoRo v{info.Version}. Multi-Instance: {info.MultiInstanceState}" +
                   (info.MultiInstanceEnabled ? "" : " (disabled)") + ".";
        });

    /// <summary>Every failure is a readable answer — see the class doc.</summary>
    internal static async Task<string> Guard(Func<Task<string>> tool)
    {
        try
        {
            return await tool().ConfigureAwait(false);
        }
        catch (ResolutionException ex) { return ex.Message; }
        catch (ConsentDeniedException ex) { return ex.Message; }
        catch (HostUnavailableException ex) { return ex.Message; }
        catch (BridgeUnavailableException ex) { return ex.Message; }
    }
}
