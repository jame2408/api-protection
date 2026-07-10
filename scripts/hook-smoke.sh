#!/usr/bin/env bash
# hook-smoke.sh — parity smoke test for the shared Claude Code/Codex hooks.
#
# Covers the ADR-008 session-context contract plus ADR-023 payload parity:
# session dedup, four C# write guards, two Bash guards, post-edit syntax
# validation, and failure-log secret scrubbing. All fixtures call the same
# scripts/agent/hook.py dispatcher used by both harness configs.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOOK="$REPO_ROOT/scripts/agent/hook.py"
LESSONS_DIR="$REPO_ROOT/tasks/lessons"

fail() {
  echo "[hook-smoke] FAIL: $1" >&2
  exit 1
}

run_hook() {
  action="$1"
  payload="$2"
  printf '%s\n' "$payload" | python3 "$HOOK" "$action"
}

expect_block() {
  action="$1"
  payload="$2"
  expected="$3"
  label="$4"

  output=$(run_hook "$action" "$payload" 2>&1)
  status=$?
  [ "$status" -eq 2 ] || fail "$label expected exit 2, got $status: $output"
  printf '%s\n' "$output" | grep -qF "$expected" \
    || fail "$label missing expected feedback '$expected': $output"
}

expect_allow() {
  action="$1"
  payload="$2"
  label="$3"

  output=$(run_hook "$action" "$payload" 2>&1)
  status=$?
  [ "$status" -eq 0 ] || fail "$label expected exit 0, got $status: $output"
}

MARKER_A=""
MARKER_C=""
MARKER_D=""
TMP_DIR=""
cleanup() {
  [ -n "$MARKER_A" ] && rm -f "$MARKER_A"
  [ -n "$MARKER_C" ] && rm -f "$MARKER_C"
  [ -n "$MARKER_D" ] && rm -f "$MARKER_D"
  [ -n "$TMP_DIR" ] && rm -rf "$TMP_DIR"
}
trap cleanup EXIT

[ -x "$HOOK" ] || fail "$HOOK does not exist or is not executable"
[ -d "$LESSONS_DIR" ] || fail "$LESSONS_DIR does not exist"

# Pick a real active lesson so the injection assertion does not go stale.
LESSONS_PICK=$(LESSONS_DIR="$LESSONS_DIR" python3 -c '
import glob
import os

paths = sorted(
    p for p in glob.glob(os.path.join(os.environ["LESSONS_DIR"], "*.md"))
    if os.path.basename(p) != "_README.md"
)
picked = None
for path in paths:
    text = open(path, encoding="utf-8").read()
    if not text.startswith("---\n"):
        continue
    end = text.find("\n---\n", 4)
    if end == -1:
        continue
    frontmatter = text[4:end]
    metadata = {}
    for line in frontmatter.splitlines():
        if ":" in line:
            key, value = line.split(":", 1)
            metadata[key.strip()] = value.strip()
    if metadata.get("status") == "active":
        picked = (metadata, text[end + 5:])

if picked is None:
    raise SystemExit("no status: active lesson file found")

metadata, body = picked
title = ""
rule_line = ""
context_line = ""
for line in body.splitlines():
    if not title and line.startswith("# "):
        title = line[2:].strip()
    if line.startswith("**Rule:**"):
        rule_line = line
    if line.startswith("**Context:**"):
        context_line = line

print("### [" + metadata.get("type", "") + "] " + title)
print(rule_line)
print(context_line)
')

LAST_ACTIVE_TITLE=$(printf '%s\n' "$LESSONS_PICK" | sed -n '1p')
LAST_ACTIVE_RULE=$(printf '%s\n' "$LESSONS_PICK" | sed -n '2p')
LAST_ACTIVE_CONTEXT=$(printf '%s\n' "$LESSONS_PICK" | sed -n '3p')

[ -n "$LAST_ACTIVE_TITLE" ] || fail "could not extract an active lesson title"
[ -n "$LAST_ACTIVE_RULE" ] || fail "could not extract an active lesson Rule line"
[ -n "$LAST_ACTIVE_CONTEXT" ] || fail "could not extract an active lesson Context line"

# Session context: Claude payload, dedup, malformed fallback, and Codex payload.
MARKER_A="$(mktemp)"
CLAUDE_SESSION='{"session_id":"hook-smoke-claude-session"}'
OUTPUT_A=$(SESSION_INIT_MARKER="$MARKER_A" run_hook session-context "$CLAUDE_SESSION")
printf '%s\n' "$OUTPUT_A" | grep -qF "必讀規範" \
  || fail "Claude session payload missing 必讀規範"
printf '%s\n' "$OUTPUT_A" | grep -qF "多模型協調與驗證機制" \
  || fail "session context missing orchestration/verification pointer"
printf '%s\n' "$OUTPUT_A" | grep -qF "$LAST_ACTIVE_TITLE" \
  || fail "session context missing active lesson title"
printf '%s\n' "$OUTPUT_A" | grep -qF "$LAST_ACTIVE_RULE" \
  || fail "session context missing active lesson Rule"
printf '%s\n' "$OUTPUT_A" | grep -qF "$LAST_ACTIVE_CONTEXT" \
  && fail "session context must not inject lesson Context"

OUTPUT_B=$(SESSION_INIT_MARKER="$MARKER_A" run_hook session-context "$CLAUDE_SESSION")
status=$?
[ "$status" -eq 0 ] || fail "same-session dedup exited non-zero ($status)"
[ -z "$OUTPUT_B" ] || fail "same-session dedup produced output: $OUTPUT_B"

MARKER_C="$(mktemp)"
BEFORE_C=$(cat "$MARKER_C")
OUTPUT_C=$(SESSION_INIT_MARKER="$MARKER_C" run_hook session-context '{}')
printf '%s\n' "$OUTPUT_C" | grep -qF "必讀規範" \
  || fail "missing-session payload should conservatively inject"
AFTER_C=$(cat "$MARKER_C")
[ "$BEFORE_C" = "$AFTER_C" ] \
  || fail "missing-session payload unexpectedly updated marker"

MARKER_D="$(mktemp)"
CODEX_SESSION='{"hook_event_name":"UserPromptSubmit","session_id":"hook-smoke-codex-session","turn_id":"turn-1","cwd":"/tmp","model":"gpt-test","permission_mode":"default","prompt":"test"}'
OUTPUT_D=$(SESSION_INIT_MARKER="$MARKER_D" run_hook session-context "$CODEX_SESSION")
printf '%s\n' "$OUTPUT_D" | grep -qF "$LAST_ACTIVE_RULE" \
  || fail "Codex session payload missing active lesson Rule"

check_edit_pair() {
  label="$1"
  snippet="$2"
  expected="$3"

  claude_payload=$(SNIPPET="$snippet" python3 -c '
import json, os
print(json.dumps({
    "tool_name": "Edit",
    "tool_input": {
        "file_path": "/repo/backend/src/Example/ExampleHandler.cs",
        "new_string": os.environ["SNIPPET"],
    },
}))
')
  codex_payload=$(SNIPPET="$snippet" python3 -c '
import json, os
patch = "*** Begin Patch\n*** Update File: backend/src/Example/ExampleHandler.cs\n@@\n+" + os.environ["SNIPPET"] + "\n*** End Patch"
print(json.dumps({
    "hook_event_name": "PreToolUse",
    "tool_name": "apply_patch",
    "tool_input": {"command": patch},
}))
')

  expect_block pre-tool-edit "$claude_payload" "$expected" "Claude $label"
  expect_block pre-tool-edit "$codex_payload" "$expected" "Codex $label"
}

check_edit_pair "new Failure guard" 'var failure = new Failure("X");' 'new Failure(...)'
check_edit_pair "bare failure code guard" 'FailureProvider.CreateFailure("X");' 'bare-string failure code'
check_edit_pair "CancellationToken naming guard" 'Task Run(CancellationToken ct);' 'must be named `cancel`'
check_edit_pair "ILogger boundary guard" 'ILogger<ExampleHandler> logger;' 'ILogger must not be injected'

# Codex must inspect added lines only, not unchanged patch context.
CONTEXT_ONLY=$(python3 -c '
import json
patch = "*** Begin Patch\n*** Update File: backend/src/Example/ExampleHandler.cs\n@@\n+// safe addition\n var old = new Failure(\"legacy context\");\n*** End Patch"
print(json.dumps({"tool_name": "apply_patch", "tool_input": {"command": patch}}))
')
expect_allow pre-tool-edit "$CONTEXT_ONLY" "Codex unchanged-context edit guard"

# Bash payload shape is common; include both the minimal Claude and full Codex fields.
CLAUDE_HEREDOC=$(python3 -c '
import json
print(json.dumps({"tool_name": "Bash", "tool_input": {"command": "python3 - <<EOF\nprint(1)\nEOF"}}))
')
CODEX_HEREDOC=$(python3 -c '
import json
print(json.dumps({"hook_event_name": "PreToolUse", "tool_name": "Bash", "tool_input": {"command": "python3 - <<EOF\nprint(1)\nEOF"}}))
')
expect_block pre-tool-bash "$CLAUDE_HEREDOC" 'heredoc' "Claude heredoc guard"
expect_block pre-tool-bash "$CODEX_HEREDOC" 'heredoc' "Codex heredoc guard"

CLAUDE_EQUALS='{"tool_name":"Bash","tool_input":{"command":"echo =="}}'
CODEX_EQUALS='{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"echo =="}}'
expect_block pre-tool-bash "$CLAUDE_EQUALS" '=word' "Claude bare-equals guard"
expect_block pre-tool-bash "$CODEX_EQUALS" '=word' "Codex bare-equals guard"
expect_allow pre-tool-bash '{"tool_name":"Bash","tool_input":{"command":"echo '\''==='\''; cat <<< value"}}' "quoted equals and herestring"

# Post-edit syntax validation for Claude file_path and Codex patch path extraction.
TMP_DIR="$(mktemp -d)"
INVALID_JSON="$TMP_DIR/invalid.json"
printf '{' > "$INVALID_JSON"
CLAUDE_POST=$(FILE_PATH="$INVALID_JSON" python3 -c '
import json, os
print(json.dumps({"tool_name": "Write", "tool_input": {"file_path": os.environ["FILE_PATH"]}}))
')
CODEX_POST=$(FILE_PATH="$INVALID_JSON" python3 -c '
import json, os
patch = "*** Begin Patch\n*** Update File: " + os.environ["FILE_PATH"] + "\n@@\n+{\n*** End Patch"
print(json.dumps({"hook_event_name": "PostToolUse", "tool_name": "apply_patch", "tool_input": {"command": patch}}))
')
expect_block post-edit-validate "$CLAUDE_POST" 'JSON parse' "Claude post-edit JSON validation"
expect_block post-edit-validate "$CODEX_POST" 'JSON parse' "Codex post-edit JSON validation"

printf '{}\n' > "$INVALID_JSON"
expect_allow post-edit-validate "$CLAUDE_POST" "Claude valid JSON post-edit"
expect_allow post-edit-validate "$CODEX_POST" "Codex valid JSON post-edit"

path_payload() {
  FILE_PATH="$1" python3 -c '
import json, os
print(json.dumps({"tool_name": "Write", "tool_input": {"file_path": os.environ["FILE_PATH"]}}))
'
}

INVALID_SH="$TMP_DIR/invalid.sh"
printf 'if then\n' > "$INVALID_SH"
expect_block post-edit-validate "$(path_payload "$INVALID_SH")" 'bash syntax check' "shell syntax validation"

INVALID_PY="$TMP_DIR/invalid.py"
printf 'def broken(:\n' > "$INVALID_PY"
expect_block post-edit-validate "$(path_payload "$INVALID_PY")" 'Python compile' "Python syntax validation"

UNSAFE_XML="$TMP_DIR/unsafe.xml"
printf '<!DOCTYPE root><root/>\n' > "$UNSAFE_XML"
expect_block post-edit-validate "$(path_payload "$UNSAFE_XML")" 'XML safety check' "XML entity safety validation"

# A Codex patch may touch multiple files; validation must not stop at the first path.
CODEX_MULTI_POST=$(JSON_PATH="$INVALID_JSON" SH_PATH="$INVALID_SH" python3 -c '
import json, os
patch = "\n".join([
    "*** Begin Patch",
    "*** Update File: " + os.environ["JSON_PATH"],
    "@@",
    "+{}",
    "*** Update File: " + os.environ["SH_PATH"],
    "@@",
    "+if then",
    "*** End Patch",
])
print(json.dumps({"hook_event_name": "PostToolUse", "tool_name": "apply_patch", "tool_input": {"command": patch}}))
')
expect_block post-edit-validate "$CODEX_MULTI_POST" 'bash syntax check' "Codex multi-file syntax validation"

# Failure observation: both payloads share the scrubber; Codex success is ignored.
FAILURES="$TMP_DIR/failures.jsonl"
SECRET='sk-1234567890abcdefghijklmnop'
CLAUDE_FAILURE=$(SECRET="$SECRET" python3 -c '
import json, os
print(json.dumps({
    "session_id": "claude-session",
    "tool_name": "Bash",
    "tool_use_id": "claude-tool",
    "tool_input": {"api_key": os.environ["SECRET"]},
    "error": "Exit code 1\n" + os.environ["SECRET"],
    "is_interrupt": False,
}))
')
CODEX_FAILURE=$(SECRET="$SECRET" python3 -c '
import json, os
print(json.dumps({
    "hook_event_name": "PostToolUse",
    "session_id": "codex-session",
    "tool_name": "Bash",
    "tool_use_id": "codex-tool",
    "tool_input": {"command": "echo " + os.environ["SECRET"]},
    "tool_response": {"exit_code": 1, "output": os.environ["SECRET"]},
}))
')
CODEX_SUCCESS='{"hook_event_name":"PostToolUse","session_id":"codex-session","tool_name":"Bash","tool_input":{"command":"true"},"tool_response":{"exit_code":0,"output":"ok"}}'

AGENT_FAILURE_FILE="$FAILURES" run_hook observe-tool-failure "$CLAUDE_FAILURE" >/dev/null \
  || fail "Claude failure observer exited non-zero"
AGENT_FAILURE_FILE="$FAILURES" run_hook observe-tool-failure "$CODEX_FAILURE" >/dev/null \
  || fail "Codex failure observer exited non-zero"
AGENT_FAILURE_FILE="$FAILURES" run_hook observe-tool-failure "$CODEX_SUCCESS" >/dev/null \
  || fail "Codex success observer exited non-zero"

[ "$(wc -l < "$FAILURES" | tr -d ' ')" -eq 2 ] \
  || fail "failure observer should write exactly two failed records"
grep -qF "$SECRET" "$FAILURES" && fail "failure observer leaked a secret value"
[ "$(grep -o '\*\*REDACTED\*\*' "$FAILURES" | wc -l | tr -d ' ')" -ge 4 ] \
  || fail "failure observer did not redact key-based and value-based secrets"

echo "[hook-smoke] all shared Claude/Codex assertions passed"
