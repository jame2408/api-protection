#!/usr/bin/env bash
# Hook: UserPromptSubmit
# Purpose: On the first prompt of each session, inject (1) a must-read rules
# reminder and (2) tasks/lessons.md, so Claude has the project's rules and prior
# lessons in context without needing to be reminded.

# Read hook input from stdin
INPUT=$(cat)

# Determine project root: this script lives at <project>/.claude/hooks/, so
# go up two levels (hooks -> .claude -> project root).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LESSONS_FILE="$PROJECT_ROOT/tasks/lessons.md"

# Only inject on first prompt of a session (transcript has 1 message = new session)
# The transcript path is taken from the hook payload and passed to Python via
# an environment variable — never interpolated into Python source — so a path
# containing quotes, backslashes, or Python syntax cannot inject code.
TRANSCRIPT_PATH=$(python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('transcript_path',''))" <<<"$INPUT" 2>/dev/null)

if [ -n "$TRANSCRIPT_PATH" ] && [ -f "$TRANSCRIPT_PATH" ]; then
  # Count human turns in transcript to detect first prompt
  HUMAN_TURNS=$(TRANSCRIPT_PATH="$TRANSCRIPT_PATH" python3 -c '
import json, os, sys
try:
    with open(os.environ["TRANSCRIPT_PATH"]) as f:
        data = json.load(f)
    turns = [m for m in data.get("messages", []) if m.get("role") == "user"]
    print(len(turns))
except Exception:
    print(0)
' 2>/dev/null)

  # Only inject on first human turn (new session)
  if [ "${HUMAN_TURNS:-0}" -gt "1" ]; then
    exit 0
  fi
fi

# Must-read rules reminder — always on the first turn, before any lessons.
# Uses globs + a pointer to CLAUDE.md §0 (the canonical rule) rather than a
# hardcoded file list, so it does not go stale when rule files are added.
echo "## 必讀規範（寫 backend code 前）"
echo ""
echo "寫任何 Handler / Service / Repository / Endpoint（production 或 test）之前，先載入規則 — 以 CLAUDE.md §0 Reference Loading 為準："
echo "- \`.claude/references/dotnet/*.rule.md\` 與 \`.claude/references/general/*.rule.md\`"
echo "- \`docs/adr/\` 內 Accepted 的 ADR（錯誤處理 / DI / 命名 / wire-format 決策）"
echo "- \`docs/design/api-spec.md\`（你要碰的 endpoint 章節）"
echo ""
echo "這些規則是機械化強制的，不是建議：違規會在「寫的當下」被 PreToolUse hook 擋（\`.claude/hooks/pre-tool-edit.py\`），並由 Architecture.Tests 與 \`scripts/source-lint.sh\` 在 commit / push / CI 攔下（\`scripts/ci-checks.sh\`）。未讀就動手 = 高機率被擋、來回重做。"
echo ""

# Check if lessons.md exists and has actual entries (below the --- separator)
if [ ! -f "$LESSONS_FILE" ]; then
  exit 0
fi

# Extract content after the --- separator (actual lessons)
LESSONS_CONTENT=$(awk '/^---$/{found++; next} found>=2{print}' "$LESSONS_FILE")

# If no lessons yet, skip
if [ -z "$(echo "$LESSONS_CONTENT" | tr -d '[:space:]')" ]; then
  exit 0
fi

# Output lessons as context injection
echo "## Session Context: Lessons Learned"
echo ""
echo "The following lessons have been captured from previous sessions in this project."
echo "Apply them proactively without waiting to be reminded."
echo ""
echo "$LESSONS_CONTENT"

# Also surface any unreviewed pending lessons from PostToolUse observations
PENDING_FILE="$PROJECT_ROOT/.claude/pending-lessons.jsonl"
if [ -f "$PENDING_FILE" ]; then
  PENDING_COUNT=$(grep -c '"reviewed": false' "$PENDING_FILE" 2>/dev/null || echo 0)
  if [ "${PENDING_COUNT}" -gt "0" ]; then
    echo ""
    echo "## Pending: $PENDING_COUNT unreviewed observation(s) flagged last session"
    echo "Consider running /lesson to review and capture them."
  fi
fi
