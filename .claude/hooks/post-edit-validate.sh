#!/usr/bin/env bash
# Hook: PostToolUse (matcher: Edit|Write)
# Purpose: Write-time syntax validation by file extension.
#
# Adopted from zeuikli/claude-code-workspace's post-edit.sh (see
# tasks/process-improvement-plan.md §10, P1), extended with an XML family
# check (.props/.csproj/.targets/.xml) — this repo's own NU1015 incident
# (an XML comment containing `--` silently broke a .props file; see
# tasks/lessons.md) is exactly the class of bug this closes.
#
# PostToolUse cannot roll back the write that already happened; exit 2 only
# surfaces the error to the agent immediately on stderr so it can fix it in
# the same turn, before the mistake compounds into later steps.
#
# Zero-false-positive scope only: this checks syntax validity, never style
# or project-specific rules (those live in scripts/source-lint.sh and the
# PreToolUse guard in pre-tool-edit.py).

set -u

INPUT=$(cat)

FILE_PATH=$(printf '%s' "$INPUT" | python3 -c '
import json, sys
try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(0)
path = (data.get("tool_input") or {}).get("file_path") or ""
print(path)
' 2>/dev/null)

# No path resolved, or malformed payload — nothing to validate.
if [ -z "$FILE_PATH" ]; then
  exit 0
fi

# File may have been deleted after the write (or the path is otherwise
# unreadable) — nothing to validate.
if [ ! -f "$FILE_PATH" ]; then
  exit 0
fi

case "$FILE_PATH" in
  *.sh)
    if ! ERR=$(bash -n "$FILE_PATH" 2>&1); then
      echo "[post-edit-validate] $FILE_PATH — bash syntax check (bash -n) failed:" >&2
      echo "$ERR" >&2
      exit 2
    fi
    ;;
  *.json)
    if ! ERR=$(python3 -c "import json, sys; json.load(open(sys.argv[1]))" "$FILE_PATH" 2>&1); then
      echo "[post-edit-validate] $FILE_PATH — JSON parse failed:" >&2
      echo "$ERR" >&2
      exit 2
    fi
    ;;
  *.py)
    if ! ERR=$(python3 -m py_compile "$FILE_PATH" 2>&1); then
      echo "[post-edit-validate] $FILE_PATH — Python compile (py_compile) failed:" >&2
      echo "$ERR" >&2
      exit 2
    fi
    ;;
  *.props|*.csproj|*.targets|*.xml)
    # Text-level reject first: legitimate MSBuild files never contain these,
    # so their presence alone is a hard fail. This blocks both XXE and
    # billion-laughs without pulling in a defusedxml dependency.
    if grep -qE '<!DOCTYPE|<!ENTITY' "$FILE_PATH"; then
      echo "[post-edit-validate] $FILE_PATH — rejected: contains <!DOCTYPE or <!ENTITY (XXE / entity-expansion guard; legitimate MSBuild files never need these)" >&2
      exit 2
    fi
    if ! ERR=$(python3 -c "import xml.etree.ElementTree as ET, sys; ET.parse(sys.argv[1])" "$FILE_PATH" 2>&1); then
      echo "[post-edit-validate] $FILE_PATH — XML well-formed check (xml.etree.ElementTree.parse) failed:" >&2
      echo "$ERR" >&2
      exit 2
    fi
    ;;
  *)
    exit 0
    ;;
esac

exit 0
