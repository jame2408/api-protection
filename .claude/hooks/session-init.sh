#!/usr/bin/env bash
# Hook: UserPromptSubmit
# Purpose: Once per session, inject (1) a must-read rules reminder and
# (2) the most recent entries from tasks/lessons.md, so Claude has the
# project's rules and prior lessons in context without needing to be
# reminded.
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

# Extract the most recent lessons from tasks/lessons.md. Entries are anchored
# by a literal "### [" at the start of a line — stable across template
# revisions, unlike counting "---" separators (which silently broke before;
# see ADR-008). At most the last 8 entries are injected, in their existing
# file order (never reordered).
if [ -f "$LESSONS_FILE" ]; then
  LESSONS_OUTPUT=$(awk '
    /^### \[/ { n++; anchor[n] = NR }
    { line[NR] = $0 }
    END {
      total = NR
      if (n == 0) { exit }
      start = (n > 8) ? anchor[n - 8 + 1] : anchor[1]
      for (i = start; i <= total; i++) print line[i]
      print ""
      printf("（完整 %d 條見 tasks/lessons.md）\n", n)
    }
  ' "$LESSONS_FILE")

  if [ -n "$(echo "$LESSONS_OUTPUT" | tr -d '[:space:]')" ]; then
    echo "## Session Context: Lessons Learned"
    echo ""
    echo "The following lessons have been captured from previous sessions in this project."
    echo "Apply them proactively without waiting to be reminded."
    echo ""
    echo "$LESSONS_OUTPUT"
  fi
fi

# Only record a marker when we have a real session_id; writing a marker for a
# missing session_id would spuriously suppress injection on every future call
# that also lacks one (see ADR-008 §1).
if [ -n "$SESSION_ID" ]; then
  printf '%s' "$SESSION_ID" > "$MARKER_FILE"
fi
