#!/usr/bin/env bash
# Hook: PostToolUse
# Purpose: Capture tool call observations to observations.jsonl.
# Flags errors and significant events as pending lessons for review.
#
# Secret handling: tool_input and tool_response are passed to Python via stdin
# (NOT shell-substituted into source) and scrubbed by recursively walking the
# JSON tree. This avoids shell/JSON-to-Python injection and ensures values whose
# keys look sensitive (token, secret, password, api_key, authorization, etc.)
# are redacted regardless of where they appear.

INPUT=$(cat)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OBS_FILE="$PROJECT_ROOT/.claude/observations.jsonl"
PENDING_FILE="$PROJECT_ROOT/.claude/pending-lessons.jsonl"

# Hand the entire hook payload to Python on stdin. Python parses, scrubs, and
# decides whether to flag the event as a pending lesson, then emits one line of
# JSON we use downstream:
#   {"flag": bool, "reason": "..."}
# stdout from Python is captured; the observation/pending writes happen inside
# Python where data is already validated JSON.
#
# Implementation note: we cannot use `python3 - <<PYEOF` here because that
# consumes stdin with the heredoc (the script source), leaving nothing for
# `json.load(sys.stdin)`. Instead we build the script in PY and pass the JSON
# payload via a here-string so stdin is the payload, not the source.
PY=$(cat <<'PYEOF'
import json, os, re, sys
from datetime import datetime, timezone

SENSITIVE_KEY = re.compile(
    r"(?i)(api[_-]?key|access[_-]?key|secret|token|password|passwd|authorization|bearer|client[_-]?secret|private[_-]?key)"
)
REDACTED = "**REDACTED**"

def scrub(value):
    if isinstance(value, dict):
        return {k: (REDACTED if SENSITIVE_KEY.search(k) else scrub(v)) for k, v in value.items()}
    if isinstance(value, list):
        return [scrub(v) for v in value]
    return value

try:
    payload = json.load(sys.stdin)
except Exception:
    print(json.dumps({"flag": False, "reason": ""}))
    sys.exit(0)

tool_name = payload.get("tool_name", "")
session_id = payload.get("session_id", "")
tool_input = payload.get("tool_input", {}) or {}
tool_result = payload.get("tool_response", {}) or {}

if not tool_name:
    print(json.dumps({"flag": False, "reason": ""}))
    sys.exit(0)

clean_input = scrub(tool_input)
clean_result = scrub(tool_result)
ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

obs_file = os.environ["OBS_FILE"]
pending_file = os.environ["PENDING_FILE"]

record = {
    "ts": ts,
    "session": session_id,
    "tool": tool_name,
    "input": clean_input,
    "result": clean_result,
}
with open(obs_file, "a", encoding="utf-8") as f:
    f.write(json.dumps(record, ensure_ascii=False) + "\n")

flag = False
reason = ""

if tool_name == "Bash":
    text = json.dumps(clean_result).lower()
    if "exit code" in text or "error" in text or "failed" in text:
        flag = True
        reason = "Bash command produced error/failure"

if tool_name in ("Write", "Edit"):
    file_path = clean_input.get("file_path") or clean_input.get("path") or ""
    if re.search(r"\.(json|yaml|yml|toml|env|config|csproj|sln)$", file_path):
        flag = True
        reason = f"Config/infra file modified: {file_path}"

if flag:
    pending = {
        "ts": ts,
        "session": session_id,
        "tool": tool_name,
        "reason": reason,
        "input": clean_input,
        "result": clean_result,
        "reviewed": False,
    }
    with open(pending_file, "a", encoding="utf-8") as f:
        f.write(json.dumps(pending, ensure_ascii=False) + "\n")

print(json.dumps({"flag": flag, "reason": reason}))
PYEOF
)

META=$(OBS_FILE="$OBS_FILE" PENDING_FILE="$PENDING_FILE" python3 -c "$PY" <<<"$INPUT")

# META is captured but not currently consumed by shell; kept for future use.
: "${META:=}"

exit 0
