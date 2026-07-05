#!/usr/bin/env bash
# Hook: UserPromptSubmit
# Purpose: Once per session, inject (1) a must-read rules reminder and
# (2) the title + Rule line of every "## Active" entry in tasks/lessons.md
# (Archived entries are skipped — their risk is already caught by a
# mechanized gate, see ADR-013 decision (b)), so Claude has the project's
# rules and prior lessons in context without needing to be reminded.
#
# Dedup design: keyed on the hook payload's session_id + a marker file
# (rewritten instead of the old transcript-turn-counting approach, which
# silently failed — see ADR-008 in docs/adr/).

# Read hook input from stdin
INPUT=$(cat)

# Determine project root: this script lives at <project>/.claude/hooks/, so
# go up two levels (hooks -> .claude -> project root).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LESSONS_FILE="$PROJECT_ROOT/tasks/lessons.md"

# Marker file records the session_id we last injected for. SESSION_INIT_MARKER
# lets tests point this at a scratch file; normal execution always uses the
# default path under .claude/.
MARKER_FILE="${SESSION_INIT_MARKER:-$PROJECT_ROOT/.claude/session-init.marker}"

# The raw hook payload is handed to Python via an environment variable —
# never interpolated into Python source — so a payload containing quotes,
# backslashes, or Python syntax cannot inject code (same anti-injection
# pattern this file has always used for path-like values).
SESSION_ID=$(HOOK_INPUT="$INPUT" python3 -c '
import json, os
try:
    d = json.loads(os.environ["HOOK_INPUT"])
    print(d.get("session_id", ""))
except Exception:
    pass
' 2>/dev/null)

# Conservative dedup: only skip when we have a session_id AND it matches the
# marker (i.e. we already injected for this exact session). A missing or
# unparseable session_id means we cannot tell whether this is a new session,
# so we inject anyway — mislead by over-injecting once, never by silently
# skipping — and we do not touch the marker in that case.
if [ -n "$SESSION_ID" ] && [ -f "$MARKER_FILE" ] && [ "$(cat "$MARKER_FILE" 2>/dev/null)" = "$SESSION_ID" ]; then
  exit 0
fi

# Must-read rules reminder — always on injection, before any lessons.
# Uses globs + a pointer to CLAUDE.md §0 (the canonical rule) rather than a
# hardcoded file list, so it does not go stale when rule files are added.
echo "## 必讀規範（寫 backend code 前）"
echo ""
echo "寫任何 Handler / Service / Repository / Endpoint（production 或 test）之前，先載入規則 — 以 CLAUDE.md §0 Reference Loading 為準："
echo "- \`.claude/references/dotnet/*.rule.md\` 與 \`.claude/references/general/*.rule.md\`"
echo "- \`docs/adr/\` 內 Accepted 的 ADR（錯誤處理 / DI / 命名 / wire-format 決策）"
echo "- \`docs/design/api-spec.md\`（你要碰的 endpoint 章節）"
echo "- 多模型協調與驗證機制：\`docs/orchestration.md\`、\`docs/verification-matrix.md\`"
echo ""
echo "這些規則是機械化強制的，不是建議：違規會在「寫的當下」被 PreToolUse hook 擋（\`.claude/hooks/pre-tool-edit.py\`），並由 Architecture.Tests 與 \`scripts/source-lint.sh\` 在 commit / push / CI 攔下（\`scripts/ci-checks.sh\`）。未讀就動手 = 高機率被擋、來回重做。"
echo ""

# Extract lessons from tasks/lessons.md, tiered per ADR-013 decision (b):
# the file is split into a "## Active" section (not yet backed by a
# mechanized gate) and a "## Archived" section (the described risk is now
# caught by a test/lint/hook, so the gate itself is the memory — no need to
# keep paying injection tokens for it). Only Active entries are injected,
# and only their "### [" title line + "**Rule:**" line (Context / 落地
# are dropped) — this replaces ADR-008 §2 / Implementation Rule 2's
# "most recent 8 entries, full text" design, which predates the
# Active/Archived split.
if [ -f "$LESSONS_FILE" ]; then
  ACTIVE_BLOCK=$(awk '/^## Active/ { f = 1; next } /^## Archived/ { f = 0 } f' "$LESSONS_FILE")
  ARCHIVED_BLOCK=$(awk '/^## Archived/ { f = 1; next } f' "$LESSONS_FILE")
  ACTIVE_COUNT=$(printf '%s\n' "$ACTIVE_BLOCK" | grep -c '^### \[')
  ARCHIVED_COUNT=$(printf '%s\n' "$ARCHIVED_BLOCK" | grep -c '^### \[')

  LESSONS_OUTPUT=$(printf '%s\n' "$ACTIVE_BLOCK" | awk '
    /^### \[/ { print; next }
    /^\*\*Rule:\*\*/ { print }
  ')

  if [ -n "$(echo "$LESSONS_OUTPUT" | tr -d '[:space:]')" ]; then
    echo "## Session Context: Lessons Learned"
    echo ""
    echo "The following lessons have been captured from previous sessions in this project."
    echo "Apply them proactively without waiting to be reminded."
    echo ""
    echo "$LESSONS_OUTPUT"
    echo ""
    printf '（Active %d 條，Archived %d 條 — 完整內容見 tasks/lessons.md）\n' "$ACTIVE_COUNT" "$ARCHIVED_COUNT"
  fi
fi

# Only record a marker when we have a real session_id; writing a marker for a
# missing session_id would spuriously suppress injection on every future call
# that also lacks one (see ADR-008 §1).
if [ -n "$SESSION_ID" ]; then
  printf '%s' "$SESSION_ID" > "$MARKER_FILE"
fi
