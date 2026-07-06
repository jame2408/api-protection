#!/usr/bin/env bash
# hook-smoke.sh — smoke test for .claude/hooks/session-init.sh.
#
# Exercises the marker-based dedup behaviour required by ADR-008 §1-2
# (docs/adr/), so a future silent regression of the injection logic fails a
# commit instead of going unnoticed for weeks (as happened before ADR-008):
#   (a) a new session_id injects must-read (incl. the docs/orchestration.md /
#       docs/verification-matrix.md pointer added by ADR-010) + the title
#       and Rule line of a known `status: active` lesson file under
#       tasks/lessons/ (ADR-013 decision (b) — archived entries are not
#       injected, and Context/落地 lines must not appear in the output
#       either; ADR-021 moved the carrier from a single file's two sections
#       to a directory of files with a frontmatter `status` field, the
#       judgment itself is unchanged)
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
LESSONS_DIR="$REPO_ROOT/tasks/lessons"

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

# Ground truth for the title/Rule/Context assertions: read it from a real
# `status: active` lesson file under tasks/lessons/ (picked deterministically
# — the lexicographically last active file, i.e. YYYYMMDD-slug.md sorted —
# rather than hardcoding text), so this test does not go stale as new
# lessons are added or archived. `_README.md` is not a lesson and is
# excluded by name. Parsing done in python3 (frontmatter + title + Rule/
# Context line extraction), not awk — repo scripts stay on bash-3.2-only
# builtins (no bash-4+ array-slurp builtins).
[ -d "$LESSONS_DIR" ] || fail "$LESSONS_DIR does not exist"

LESSONS_PICK=$(LESSONS_DIR="$LESSONS_DIR" python3 -c '
import glob
import os

lessons_dir = os.environ["LESSONS_DIR"]
paths = sorted(
    p for p in glob.glob(os.path.join(lessons_dir, "*.md"))
    if os.path.basename(p) != "_README.md"
)

picked = None
for path in paths:
    with open(path, "r", encoding="utf-8") as fh:
        text = fh.read()
    if not text.startswith("---\n"):
        continue
    end = text.find("\n---\n", 4)
    if end == -1:
        continue
    frontmatter = text[4:end]
    meta = {}
    for line in frontmatter.splitlines():
        if ":" in line:
            k, v = line.split(":", 1)
            meta[k.strip()] = v.strip()
    if meta.get("status") == "active":
        picked = (path, frontmatter, text[end + 5:])

if picked is None:
    raise SystemExit("no status: active lesson file found")

path, frontmatter, body = picked
title = ""
rule_line = ""
context_line = ""
lesson_type = ""
for line in frontmatter.splitlines():
    if line.startswith("type:"):
        lesson_type = line.split(":", 1)[1].strip()
for line in body.splitlines():
    if not title and line.startswith("# "):
        title = line[2:].strip()
    if line.startswith("**Rule:**"):
        rule_line = line
    if line.startswith("**Context:**"):
        context_line = line

print("### [" + lesson_type + "] " + title)
print(rule_line)
print(context_line)
')

# Three lines on stdout (title / Rule / Context) — split with sed rather
# than a bash-4+ array-slurp builtin, to stay bash 3.2 compatible.
LAST_ACTIVE_TITLE=$(printf '%s\n' "$LESSONS_PICK" | sed -n '1p')
LAST_ACTIVE_RULE=$(printf '%s\n' "$LESSONS_PICK" | sed -n '2p')
LAST_ACTIVE_CONTEXT=$(printf '%s\n' "$LESSONS_PICK" | sed -n '3p')

[ -n "$LAST_ACTIVE_TITLE" ] || fail "could not extract title of a status: active lesson file"
[ -n "$LAST_ACTIVE_RULE" ] || fail "could not extract **Rule:** line of a status: active lesson file"
[ -n "$LAST_ACTIVE_CONTEXT" ] || fail "could not extract **Context:** line of a status: active lesson file"

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
