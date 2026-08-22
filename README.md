# RoRoRo Ur MCP

Let Claude drive [RoRoRo](https://github.com/estevanhernandez-stack-ed/ROROROblox). This is a
stdio MCP server that Claude Code or Claude Desktop launches as a subprocess, authorized as an
installed, consent-gated RoRoRo plugin. Thirteen tools across two surfaces: your saved accounts
(launch, follow, status, stop) and Ur Task's macros (list, run, stop).

The design target is the recovery loop: internet drops mid-session, a Discord alert says an
account fell out of game, you remote in and say — *"Launch Pokey, Spud, and Clover. Run the
get-in-position macro on all three. Now the farm macro on repeat."* Claude is the operator,
RoRoRo launches the alts, Ur Task's hands run the macros. It is also the app-control plane for
agent-driven release smoke tests: launch → `wait_for_ingame` → `running_status` → `stop_accounts`
→ `host_info`, with the screenshot tooling riding alongside.

## The tools

| Tool | Does |
|---|---|
| `list_accounts` | Every saved account — name, uid, which is main, running state + game |
| `launch_account` | Launch by name ("Pokey") or id |
| `launch_into_game` | Launch into a share URL, game URL, or bare place id |
| `follow_main` / `follow_friend` | Follow-launch into the main's (or a friend's) server |
| `running_status` | Who's running, in what game, plus the current private server |
| `wait_for_ingame` | Poll until presence confirms a game, up to a timeout |
| `stop_accounts` | Close clients by name, or all — graceful close, then kill |
| `account_activity` | Idle time per account |
| `host_info` | RoRoRo version + Multi-Instance (mutex) state |
| `list_macros` / `run_macro` / `stop_macro` | Ur Task's macro library, with `repeat` and stop-by-playback-id |

Every failure is a readable answer, not a protocol error: "RoRoRo isn't running — open it and
try again", "consent not granted for X — grant it in RoRoRo's Plugins window", and unknown names
come back listing the real candidates.

## Setup

1. **Install the plugin in RoRoRo** (Plugins window → install from URL / marketplace). The
   consent sheet lists exactly what it can do; autostart stays **off** — Claude owns this
   process's lifecycle, RoRoRo never starts it.
2. **Register it with Claude Code:**
   ```
   claude mcp add rororo -- "%LOCALAPPDATA%\ROROROblox\plugins\626labs.ur-mcp\626labs.ur-mcp.exe"
   ```
   (Claude Desktop: the equivalent `mcpServers` stdio entry.)
3. **Talk to Claude.** RoRoRo must be running for the account tools; Ur Task must be installed
   and running for the macro tools. Each tool says so plainly when its other end is missing.

## The wall

Automation lives here, in a consent-gated, direct-download plugin — never in the Store-listed
core. The account tools are enforced per-RPC by RoRoRo's capability interceptor against what you
granted at install. The macro tools only ever *ask* Ur Task; the input-synthesis consent lives in
Ur Task's own grants, and this connector contains no input-synthesis path of any kind.

## Building

Requires the ROROROblox repo checked out as a **sibling directory** (the contract is a
ProjectReference until `ROROROblox.PluginContract` 0.9.0 is on NuGet):

```
<parent>/ROROROblox
<parent>/rororo-ur-mcp
```

```
dotnet test tests/rororo-ur-mcp.Tests/rororo-ur-mcp.Tests.csproj   # 27 tests
./build/build-plugin.ps1                                            # artifacts/: manifest.json + manifest.sha256 + plugin.zip
```

A quick stdio smoke without Claude: `npx @modelcontextprotocol/inspector dotnet run` — expect 13
tools listed.

## Provenance

Design: ROROROblox `docs/superpowers/specs/2026-07-04-mcp-connector-design.md` (approved
2026-07-04, reconciled against the trees 2026-08-22). Host-side `GetAccounts` shipped as contract
0.9.0; Ur Task's `ListMacros`/`repeat`/`StopMacro` shipped as bridge additions in ur-task 0.8.0.
A 626 Labs product.
