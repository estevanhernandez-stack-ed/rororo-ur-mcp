# Changelog

All notable changes to RoRoRo Ur MCP are documented here. Format roughly follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [SemVer](https://semver.org/).

## 0.1.0 — 2026-08-22

### Added

- **First release: thirteen tools over stdio MCP.** Accounts (list, launch, launch-into-game,
  follow-main, follow-friend, running status, wait-for-ingame, stop, idle activity, host info)
  ride RoRoRo's consent-gated plugin host; macros (list, run with repeat, stop) ride Ur Task's
  action bridge. Claude launches the process; RoRoRo authorizes it as an installed plugin —
  autostart stays off, and every failure comes back as a readable answer instead of a protocol
  error. Requires RoRoRo with plugin contract 0.9.0 and, for macros, Ur Task 0.8.0.
