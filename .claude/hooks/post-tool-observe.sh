#!/usr/bin/env bash
# Hook: PostToolUse
# Purpose: Capture tool call observations to observations.jsonl.
# Flags errors and significant events as pending lessons for review.
#
# Secret handling: tool_input and tool_response are passed to Python via stdin
# (NOT shell-substituted into source) and scrubbed by recursively walking the
# JSON tree. This avoids shell/JSON-to-Python injection and applies two layers
# of redaction:
#   1. Key-based: values whose keys look sensitive (token, secret, password,
#      api_key, authorization, etc.) are fully replaced with **REDACTED**.
#   2. Value-based: string leaves are scanned for known secret shapes (Bearer
#      tokens, sk-/sk-ant- prefixed keys, JWTs, GitHub tokens, AWS access keys)
#      and redacted in-place even when the surrounding key name looks benign
#      (e.g. embedded inside a command string or stdout).

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

# Value-level patterns: applied to every string leaf, regardless of key name.
# Targeted to well-known secret shapes to keep false positives low.
VALUE_PATTERNS = [
    # Bearer <token>: redact the token portion, keep the prefix for context.
    (re.compile(r"(?i)\b(Bearer)\s+[A-Za-z0-9._\-+/=]{8,}"), r"\1 **REDACTED**"),
    # JWT (three base64url-ish segments separated by dots, header starts with eyJ).
    (re.compile(r"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+"), REDACTED),
    # OpenAI / Anthropic style keys: sk-..., sk-ant-...
    (re.compile(r"\bsk-(?:ant-)?[A-Za-z0-9_\-]{16,}"), REDACTED),
    # GitHub tokens (PAT, OAuth, user-to-server, server-to-server, refresh).
    (re.compile(r"\bgh[pousr]_[A-Za-z0-9]{20,}"), REDACTED),
    # AWS access key id.
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
