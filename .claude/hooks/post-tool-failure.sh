#!/usr/bin/env bash
# Hook: PostToolUseFailure
# Purpose: Capture tool-call failures to failures.jsonl and flag them as
# pending lessons unless the failure was a user interrupt.
#
# The PostToolUseFailure payload differs from PostToolUse: there is no
# tool_response field; instead an `error` string and an optional
# `is_interrupt` boolean describe the failure. We reuse the same secret
# scrubbing approach (key-based + value-based regex) as post-tool-observe.sh
# but apply it to tool_input + error only.
#
# Note: the scrubber implementation is duplicated from post-tool-observe.sh.
# A future refactor can extract scrub.py into a shared library; deferred to
# keep this commit single-intent.

INPUT=$(cat)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
FAIL_FILE="$PROJECT_ROOT/.claude/failures.jsonl"
PENDING_FILE="$PROJECT_ROOT/.claude/pending-lessons.jsonl"

PY=$(cat <<'PYEOF'
import json, os, re, sys
from datetime import datetime, timezone

SENSITIVE_KEY = re.compile(
    r"(?i)(api[_-]?key|access[_-]?key|secret|token|password|passwd|authorization|bearer|client[_-]?secret|private[_-]?key)"
)
REDACTED = "**REDACTED**"

VALUE_PATTERNS = [
    (re.compile(r"(?i)\b(Bearer)\s+[A-Za-z0-9._\-+/=]{8,}"), r"\1 **REDACTED**"),
    (re.compile(r"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+"), REDACTED),
    (re.compile(r"\bsk-(?:ant-)?[A-Za-z0-9_\-]{16,}"), REDACTED),
    (re.compile(r"\bgh[pousr]_[A-Za-z0-9]{20,}"), REDACTED),
    (re.compile(r"\bAKIA[0-9A-Z]{16}\b"), REDACTED),
]

def scrub_string(s):
    out = s
    for pat, repl in VALUE_PATTERNS:
        out = pat.sub(repl, out)
    return out

def scrub(value):
    if isinstance(value, dict):
        return {k: (REDACTED if SENSITIVE_KEY.search(k) else scrub(v)) for k, v in value.items()}
    if isinstance(value, list):
        return [scrub(v) for v in value]
    if isinstance(value, str):
        return scrub_string(value)
    return value

try:
    payload = json.load(sys.stdin)
except Exception:
    sys.exit(0)

tool_name = payload.get("tool_name", "")
session_id = payload.get("session_id", "")
tool_input = payload.get("tool_input", {}) or {}
error = payload.get("error", "") or ""
is_interrupt = bool(payload.get("is_interrupt", False))
duration_ms = payload.get("duration_ms")
tool_use_id = payload.get("tool_use_id", "")

if not tool_name:
    sys.exit(0)

clean_input = scrub(tool_input)
clean_error = scrub_string(error) if isinstance(error, str) else scrub(error)
ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

fail_file = os.environ["FAIL_FILE"]
pending_file = os.environ["PENDING_FILE"]

record = {
    "ts": ts,
    "session": session_id,
    "tool": tool_name,
    "tool_use_id": tool_use_id,
    "input": clean_input,
    "error": clean_error,
    "is_interrupt": is_interrupt,
}
if duration_ms is not None:
    record["duration_ms"] = duration_ms

with open(fail_file, "a", encoding="utf-8") as f:
    f.write(json.dumps(record, ensure_ascii=False) + "\n")

# User interrupts are not lessons — skip flagging.
if is_interrupt:
    sys.exit(0)

pending = {
    "ts": ts,
    "session": session_id,
    "tool": tool_name,
    "reason": f"Tool failure: {tool_name}",
    "input": clean_input,
    "error": clean_error,
    "reviewed": False,
}
with open(pending_file, "a", encoding="utf-8") as f:
    f.write(json.dumps(pending, ensure_ascii=False) + "\n")
PYEOF
)

FAIL_FILE="$FAIL_FILE" PENDING_FILE="$PENDING_FILE" python3 -c "$PY" <<<"$INPUT"

exit 0
