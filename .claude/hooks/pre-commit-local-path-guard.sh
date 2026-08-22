#!/usr/bin/env bash
# RoRoRo Ur Task — pre-commit local-path guard
# Fails the commit if any staged file contains a Windows user-profile path (c:\Users\<name>\).
# Per pattern kk from wbp-azure: a c:\Users\ reference in committable code breaks CI on every
# machine that isn't yours.
# Triggered as a git pre-commit hook (see .claude/hooks/install.ps1).

set -euo pipefail

red() { printf "\033[31m%s\033[0m\n" "$*" >&2; }
green() { printf "\033[32m%s\033[0m\n" "$*"; }

# SCAN_ALL=1 (CI mode): scan every tracked file instead of the staged diff — same rationale
# as the secret scan's CI twin (hooks are per-machine and bypassable).
if [ "${SCAN_ALL:-0}" = "1" ]; then
  staged=$(git ls-files)
else
  staged=$(git diff --cached --name-only --diff-filter=ACM)
fi
if [ -z "$staged" ]; then
  exit 0
fi

# Documentation files that legitimately reference user-profile paths (working dir, project root).
# Add to this list with care — only files where the path is the documentation, not a code dependency.
allow=(
  "CLAUDE.md"
  # Frozen session records, not build instructions. These describe what was actually run on a
  # specific machine on a specific day; rewriting the paths to satisfy this guard would falsify
  # the record. Listed individually rather than by directory so a NEW doc still gets caught and
  # somebody has to make the call consciously.
  ".vibe-cartographer/checklist.md"
  "docs/session-handoff/2026-05-12-evening-rc15-wrap.md"
  "docs/session-handoff/2026-05-12-rc9-wrap.md"
  "docs/superpowers/plans/2026-05-11-v0.2-implementation.md"
)

# Directory prefixes where absolute paths ARE the documentation (review reports cite
# machine-local file:line evidence).
allow_prefixes=(
  # The guards themselves. A path-pattern matcher necessarily CONTAINS the pattern it hunts for,
  # in its grep expression and in the comments explaining it — likewise the secret scan and its
  # cookie prefixes. Excluding this directory is not a loophole; including it makes the guard
  # unable to be committed at all. (The host repo never hit this because its hooks were committed
  # before the hook was installed.)
  ".claude/hooks/"
)

violations=0

while IFS= read -r file; do
  [ -z "$file" ] && continue
  [ ! -f "$file" ] && continue

  # Skip allowlisted files
  is_allowed=0
  for allowed in "${allow[@]}"; do
    if [ "$file" = "$allowed" ]; then
      is_allowed=1
      break
    fi
  done
  for prefix in "${allow_prefixes[@]}"; do
    case "$file" in
      "$prefix"*) is_allowed=1 ;;
    esac
  done
  [ "$is_allowed" -eq 1 ] && continue

  # Match c:\Users\ or C:/Users/ in any case.
  # -I skips binary files — compiled artifacts (.exe, .pdb, .dll) often have build-path strings
  # baked in by the toolchain that aren't deployment-relevant.
  if grep -IinE "([cC]:\\\\[uU][sS][eE][rR][sS]\\\\|[cC]:/[uU][sS][eE][rR][sS]/)" "$file" >/dev/null 2>&1; then
    red "[local-path-guard] FAIL: $file contains a c:\\Users\\ reference."
    grep -IinE "([cC]:\\\\[uU][sS][eE][rR][sS]\\\\|[cC]:/[uU][sS][eE][rR][sS]/)" "$file" | sed 's/^/  /' >&2
    violations=$((violations + 1))
  fi
done <<< "$staged"

if [ "$violations" -gt 0 ]; then
  red ""
  red "[local-path-guard] $violations file(s) with hardcoded user-profile paths. Commit blocked."
  red "[local-path-guard] Replace with relative paths or env vars (%LOCALAPPDATA%, %USERPROFILE%, $HOME)."
  red "[local-path-guard] If this is intentional documentation only, add the file to the allowlist in"
  red "[local-path-guard] .claude/hooks/pre-commit-local-path-guard.sh."
  exit 1
fi

green "[local-path-guard] clean — no c:\\Users\\ paths in staged files."
exit 0
