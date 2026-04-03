#!/usr/bin/env bash
# Hook: UserPromptSubmit
# Purpose: Inject tasks/lessons.md into the first prompt of each session
# so Claude always has lessons context without needing to be reminded.

# Read hook input from stdin
INPUT=$(cat)

# Determine project root (same dir as this hook's .claude parent)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LESSONS_FILE="$PROJECT_ROOT/tasks/lessons.md"

# Only inject on first prompt of a session (transcript has 1 message = new session)
TRANSCRIPT_PATH=$(echo "$INPUT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('transcript_path',''))" 2>/dev/null)

if [ -n "$TRANSCRIPT_PATH" ] && [ -f "$TRANSCRIPT_PATH" ]; then
  # Count human turns in transcript to detect first prompt
  HUMAN_TURNS=$(python3 -c "
import json, sys
try:
    with open('$TRANSCRIPT_PATH') as f:
        data = json.load(f)
    turns = [m for m in data.get('messages', []) if m.get('role') == 'user']
    print(len(turns))
except:
    print(0)
" 2>/dev/null)

  # Only inject on first human turn (new session)
  if [ "${HUMAN_TURNS:-0}" -gt "1" ]; then
    exit 0
  fi
fi

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
