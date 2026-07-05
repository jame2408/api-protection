#!/usr/bin/env bash
# hook-smoke.sh — smoke test for .claude/hooks/session-init.sh.
#
# Exercises the marker-based dedup behaviour required by ADR-008 §1-2
# (docs/adr/), so a future silent regression of the injection logic fails a
# commit instead of going unnoticed for weeks (as happened before ADR-008):
#   (a) a new session_id injects must-read (incl. the docs/orchestration.md /
#       docs/verification-matrix.md pointer added by ADR-010) + the title
#       and Rule line of the last "## Active" entry in tasks/lessons.md
#       (ADR-013 decision (b) — Archived entries are not injected, and
#       Context/落地 lines must not appear in the output either)
#   (b) the same session_id run again produces no output, exit 0
#   (c) a payload missing session_id still injects (conservative fallback)
#
# Uses scratch marker files (SESSION_INIT_MARKER) so it never touches the
# real .claude/session-init.marker used by live sessions.
#
# Usage:
#   scripts/hook-smoke.sh
#
# Exit code:
#   0 — all assertions pass
#   1 — at least one assertion failed (message printed to stderr)
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOOK="$REPO_ROOT/.claude/hooks/session-init.sh"
LESSONS_FILE="$REPO_ROOT/tasks/lessons.md"

fail() {
  echo "[hook-smoke] FAIL: $1" >&2
  exit 1
}

MARKER_A=""
MARKER_C=""
cleanup() {
  [ -n "$MARKER_A" ] && rm -f "$MARKER_A"
  [ -n "$MARKER_C" ] && rm -f "$MARKER_C"
}
trap cleanup EXIT

# Ground truth for the "last Active entry" assertions: read it from the real
# lessons.md rather than hardcoding text, so this test does not go stale as
# new lessons are appended or archived. Only the "## Active" section (before
# "## Archived") is in scope — see ADR-013 decision (b).
ACTIVE_BLOCK=$(awk '/^## Active/ { f = 1; next } /^## Archived/ { f = 0 } f' "$LESSONS_FILE")
[ -n "$(printf '%s' "$ACTIVE_BLOCK" | grep '^### \[')" ] || fail "could not find any '### [' entry under '## Active' in $LESSONS_FILE to assert against"

LAST_ACTIVE_ENTRY=$(printf '%s\n' "$ACTIVE_BLOCK" | awk '
  /^### \[/ { n++ }
  { buf[n] = buf[n] $0 "\n" }
  END { printf "%s", buf[n] }
')
LAST_ACTIVE_TITLE=$(printf '%s\n' "$LAST_ACTIVE_ENTRY" | grep '^### \[')
LAST_ACTIVE_RULE=$(printf '%s\n' "$LAST_ACTIVE_ENTRY" | grep '^\*\*Rule:\*\*')
LAST_ACTIVE_CONTEXT=$(printf '%s\n' "$LAST_ACTIVE_ENTRY" | grep '^\*\*Context:\*\*')
[ -n "$LAST_ACTIVE_TITLE" ] || fail "could not extract title of last '## Active' entry"
[ -n "$LAST_ACTIVE_RULE" ] || fail "could not extract **Rule:** line of last '## Active' entry"
[ -n "$LAST_ACTIVE_CONTEXT" ] || fail "could not extract **Context:** line of last '## Active' entry"

# --- (a) new session_id -> inject must-read + last Active entry's title+Rule
MARKER_A="$(mktemp)"
OUTPUT_A=$(echo '{"session_id":"hook-smoke-session-1"}' | SESSION_INIT_MARKER="$MARKER_A" bash "$HOOK")
echo "$OUTPUT_A" | grep -qF "必讀規範" || fail "(a) missing 必讀規範 in first-run output"
# Match the unique phrase from the must-read pointer line itself (ADR-010), not
# just "docs/orchestration.md" — that substring can also appear incidentally
# inside injected lessons text, which would make this assertion pass even if
# the must-read line were deleted.
echo "$OUTPUT_A" | grep -qF "多模型協調與驗證機制" || fail "(a) missing 多模型協調與驗證機制 pointer (docs/orchestration.md / docs/verification-matrix.md) in first-run output (ADR-010)"
echo "$OUTPUT_A" | grep -qF "$LAST_ACTIVE_TITLE" || fail "(a) missing last Active entry's title in first-run output"
echo "$OUTPUT_A" | grep -qF "$LAST_ACTIVE_RULE" || fail "(a) missing last Active entry's Rule line in first-run output"
echo "$OUTPUT_A" | grep -qF "$LAST_ACTIVE_CONTEXT" && fail "(a) output must NOT contain the last Active entry's Context line (ADR-013 decision (b): only title + Rule are injected)"

# --- (b) same session_id again -> marker dedup, no output, exit 0 ---------
OUTPUT_B=$(echo '{"session_id":"hook-smoke-session-1"}' | SESSION_INIT_MARKER="$MARKER_A" bash "$HOOK")
STATUS_B=$?
[ "$STATUS_B" -eq 0 ] || fail "(b) second run for the same session_id exited non-zero ($STATUS_B)"
[ -z "$OUTPUT_B" ] || fail "(b) second run for the same session_id produced output, expected empty: <<<$OUTPUT_B>>>"

# --- (c) payload missing session_id -> conservative: inject, don't touch marker
MARKER_C="$(mktemp)"
BEFORE_C=$(cat "$MARKER_C")
OUTPUT_C=$(echo '{}' | SESSION_INIT_MARKER="$MARKER_C" bash "$HOOK")
echo "$OUTPUT_C" | grep -qF "必讀規範" || fail "(c) payload missing session_id should still inject must-read"
AFTER_C=$(cat "$MARKER_C")
[ "$BEFORE_C" = "$AFTER_C" ] || fail "(c) marker file was updated despite a missing session_id"

echo "[hook-smoke] all assertions passed"
