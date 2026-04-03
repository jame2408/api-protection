#!/usr/bin/env bash
# Hook: PostToolUse
# Purpose: Capture tool call observations to observations.jsonl.
# Flags errors and significant events as pending lessons for review.

INPUT=$(cat)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
OBS_FILE="$PROJECT_ROOT/.claude/observations.jsonl"
PENDING_FILE="$PROJECT_ROOT/.claude/pending-lessons.jsonl"

# Parse hook input
TOOL_NAME=$(echo "$INPUT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('tool_name',''))" 2>/dev/null)
SESSION_ID=$(echo "$INPUT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('session_id',''))" 2>/dev/null)
TOOL_INPUT=$(echo "$INPUT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(json.dumps(d.get('tool_input',{})))" 2>/dev/null)
TOOL_RESULT=$(echo "$INPUT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(json.dumps(d.get('tool_response',{})))" 2>/dev/null)

# Skip internal/observer sessions
[ -z "$TOOL_NAME" ] && exit 0

TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# Scrub secrets from tool input before writing
TOOL_INPUT_CLEAN=$(echo "$TOOL_INPUT" | sed -E \
  -e 's/(api[_-]?key|token|password|secret|authorization)[\"'\'']*\s*[:=]\s*[\"'\'']*[^\"'\'',}\s]+/\1=**REDACTED**/gi')

# Write observation record
python3 - <<PYEOF
import json, os
record = {
    "ts": "$TIMESTAMP",
    "session": "$SESSION_ID",
    "tool": "$TOOL_NAME",
    "input": $TOOL_INPUT_CLEAN,
    "result": $TOOL_RESULT
}
with open("$OBS_FILE", "a", encoding="utf-8") as f:
    f.write(json.dumps(record, ensure_ascii=False) + "\n")
PYEOF

# --- Detect significant events worth flagging as pending lessons ---

SHOULD_FLAG=0
FLAG_REASON=""

# 1. Bash command failed (non-zero exit code in result)
if [ "$TOOL_NAME" = "Bash" ]; then
  EXIT_CODE=$(echo "$TOOL_RESULT" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    # result may be string or object
    content = d.get('content', d) if isinstance(d, dict) else d
    text = str(content)
    # look for exit code signals
    if 'exit code' in text.lower() or 'error' in text.lower() or 'failed' in text.lower():
        print('1')
    else:
        print('0')
except:
    print('0')
" 2>/dev/null)

  if [ "${EXIT_CODE}" = "1" ]; then
    SHOULD_FLAG=1
    FLAG_REASON="Bash command produced error/failure"
  fi
fi

# 2. Write tool used on a test/config file (architectural decision)
if [ "$TOOL_NAME" = "Write" ] || [ "$TOOL_NAME" = "Edit" ]; then
  FILE_PATH=$(echo "$TOOL_INPUT_CLEAN" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    print(d.get('file_path', d.get('path', '')))
except:
    print('')
" 2>/dev/null)

  # Flag config/infra file changes
  if echo "$FILE_PATH" | grep -qE '\.(json|yaml|yml|toml|env|config|csproj|sln)$'; then
    SHOULD_FLAG=1
    FLAG_REASON="Config/infra file modified: $FILE_PATH"
  fi
fi

# Write pending lesson if flagged
if [ "$SHOULD_FLAG" = "1" ]; then
  python3 - <<PYEOF
import json, os
record = {
    "ts": "$TIMESTAMP",
    "session": "$SESSION_ID",
    "tool": "$TOOL_NAME",
    "reason": "$FLAG_REASON",
    "input": $TOOL_INPUT_CLEAN,
    "result": $TOOL_RESULT,
    "reviewed": False
}
with open("$PENDING_FILE", "a", encoding="utf-8") as f:
    f.write(json.dumps(record, ensure_ascii=False) + "\n")
PYEOF
fi

exit 0
