# Changelog

All notable changes to RoRoRo Ur MCP are documented here. Format roughly follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [SemVer](https://semver.org/).

## 0.5.0 — 2026-10-25

### Added

- **`keepalive_status` tool.** Reads Ur Task's new `GetStatus` and answers in one glance: whether
  keep-alive is running (and turbo), then one line per account with its idle time, the time it
  needs input by, and its misses, e.g. `Pokey: idle 6m, needs input by 15:42 (keep-alive), misses 0`.
  Accounts with keep-alive off say so, a macro paused for keep-alive is named, and the last miss
  is appended when there is one. Use it before a long run to see who needs a Space and when.
  Needs Ur Task 0.13.0 or later; an older Ur Task gets "Update Ur Task to 0.13.0 or later to see
  keep-alive status." instead of an error.

## 0.4.1 — 2026-10-23

### Fixed

- **The 0.4.0 release reported itself as 0.3.0.** The binary was 0.4.0, but `manifest.json` was
  never bumped, and the manifest is what RoRoRo reads (it ships inside `plugin.zip` as well as
  beside it). Once RoRoRo's catalog advertised 0.4.0, a direct-download host would have offered
  an update that could never clear, because installing it delivered another 0.3.0 manifest. No
  code change from 0.4.0.
- **The build now refuses a version mismatch.** `build/build-plugin.ps1` fails when
  `manifest.json` and the csproj `<Version>` disagree, and in CI when the tag is not
  `v<version>`, so this cannot ship again.


## 0.4.0 — 2026-10-19

### Changed

- **Contract 0.12.0.** `ROROROblox.PluginContract` 0.9.0 to 0.12.0.
- **`host_info` reports build identity.** After the version and Multi-Instance sentence it now
  adds `Build: store|sideload|dev.`, `Informational version: <version+commit hash>.` and
  `Process: <host exe path>.`, so an agent can tell which RoRoRo binary it is driving (a dev
  build and the Store host used to report the same string). Hosts on contract 0.11 or older say
  "Build identity: not reported by this host (contract 0.11 or older)."


## 0.3.0 — 2026-10-11

### Added

- **`list_macros`: `query` and `group`, plus grouped output.** `query` keeps macros whose name
  contains every word (case-insensitive); `group` keeps one group, the text before the first
  " | " in the name (names without one are "Ungrouped"). Output is grouped, route steps like
  "booth 1/3" first in step order. Over 60 macros with no filter, it hints at the new
  parameters. Filtering happens in the MCP, so any Ur Task version works unchanged.


## 0.2.0 — 2026-09-27

### Added

- **`wait_for_macro`: how a macro playback ended.** Polls Ur Task's new `GetPlayback` bridge
  method by the playback id `run_macro` returned, and answers finished, stopped, or failed with
  Ur Task's reason (a colour check that didn't match, say). `timeoutSeconds: 0` checks once.
  Requires Ur Task 0.9.0; against an older Ur Task it says the Ur Task is too old, instead of erroring.

## 0.1.0 — 2026-08-22

### Added

- **First release: thirteen tools over stdio MCP.** Accounts (list, launch, launch-into-game,
  follow-main, follow-friend, running status, wait-for-ingame, stop, idle activity, host info)
  ride RoRoRo's consent-gated plugin host; macros (list, run with repeat, stop) ride Ur Task's
  action bridge. Claude launches the process; RoRoRo authorizes it as an installed plugin —
  autostart stays off, and every failure comes back as a readable answer instead of a protocol
  error. Requires RoRoRo with plugin contract 0.9.0 and, for macros, Ur Task 0.8.0.
