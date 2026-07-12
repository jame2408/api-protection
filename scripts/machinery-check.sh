#!/usr/bin/env bash
# machinery-check.sh — self-check for the governance machinery itself
# (Claude/Codex wiring, shared hook dispatcher, skill links, and pointers).
#
# Adopted from zeuikli/claude-code-workspace's healthcheck.sh (see
# tasks/archive/process-improvement-plan.md §10, P2) — rewritten fail-loud.
# That repo's own healthcheck used `if [ -f ]` to silently skip missing files,
# which is exactly the anti-pattern this check exists to prevent (see
# tasks/archive/process-improvement-plan.md §10.1, last row). Every check
# below is either pass or hard-fail; nothing is silently skipped.
#
# Checks:
#   1. .claude/settings.json and .codex/hooks.json are valid JSON; .mcp.json
#      too, if present.
#   2. Both harness configs wire every ADR-023 action to the one shared,
#      executable, py_compile-clean scripts/agent/hook.py dispatcher.
#   3. Every scripts/**/*.sh file passes bash -n.
#   4. Pointer integrity: every backtick-wrapped path in CLAUDE.md,
#      AGENTS.md, docs/orchestration.md, docs/verification-matrix.md matching
#      a repo-owned documentation/code/config path must exist.
#      Glob-style entries (containing `*`) are skipped;
#      a line containing the literal marker "machinery-check:ignore" is
#      exempt (same convention as zh-lint:allow); gitignored paths are
#      exempt (machine-local by design — they legitimately don't exist in
#      a fresh CI checkout, e.g. .claude/settings.local.json).
#   5. Every tracked .claude project skill has a matching .agents/skills
#      symlink resolving to the same directory; unrelated third-party links
#      are ignored.
#
# Exit code: 0 — all checks pass; 1 — at least one failure (printed to stderr).
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

status=0

fail() {
  echo "[machinery-check] FAIL: $1" >&2
  status=1
}

# --- 1. harness config / optional .mcp.json JSON validity ------------------
for config in .claude/settings.json .codex/hooks.json; do
  if ! ERR=$(python3 -c "import json, sys; json.load(open(sys.argv[1]))" "$config" 2>&1); then
    fail "$config is not valid JSON: $ERR"
  fi
done

# .mcp.json is optional tooling config (Tessl et al., see
# tasks/archive/process-improvement-plan.md §9.3 D-2) — checking "if present" here is
# the spec's own conditional, not a silent skip of something required to exist.
if [ -f .mcp.json ]; then
  if ! ERR=$(python3 -c "import json; json.load(open('.mcp.json'))" 2>&1); then
    fail ".mcp.json is not valid JSON: $ERR"
  fi
fi

# --- 2. shared dispatcher + complete harness wiring ------------------------
HOOK_CHECK=$(python3 - <<'PY'
import json
import os
import shlex
import subprocess
import sys

DISPATCHER = "scripts/agent/hook.py"
EXPECTED = {
    ".claude/settings.json": {
        ("UserPromptSubmit", "session-context"),
        ("PreToolUse", "pre-tool-edit"),
        ("PreToolUse", "pre-tool-bash"),
        ("PostToolUse", "post-edit-validate"),
        ("PostToolUseFailure", "observe-tool-failure"),
    },
    ".codex/hooks.json": {
        ("UserPromptSubmit", "session-context"),
        ("PreToolUse", "pre-tool-edit"),
        ("PreToolUse", "pre-tool-bash"),
        ("PostToolUse", "post-edit-validate"),
        ("PostToolUse", "observe-tool-failure"),
    },
}

status = 0
if not os.path.isfile(DISPATCHER):
    print(f"shared hook dispatcher missing: {DISPATCHER}")
    status = 1
elif not os.access(DISPATCHER, os.X_OK):
    print(f"shared hook dispatcher not executable: {DISPATCHER}")
    status = 1
else:
    result = subprocess.run(
        ["python3", "-m", "py_compile", DISPATCHER],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        print(f"{DISPATCHER} failed py_compile: {result.stderr.strip()}")
        status = 1

for config_path, expected in EXPECTED.items():
    with open(config_path, encoding="utf-8") as stream:
        hooks = json.load(stream).get("hooks") or {}

    found = set()
    for event, groups in hooks.items():
        for group in groups:
            for hook in group.get("hooks", []):
                command = hook.get("command", "")
                if DISPATCHER not in command:
                    print(f"{config_path} {event} hook does not use {DISPATCHER}: {command}")
                    status = 1
                    continue
                try:
                    action = shlex.split(command)[-1]
                except ValueError as error:
                    print(f"{config_path} {event} hook command cannot be parsed: {error}")
                    status = 1
                    continue
                found.add((event, action))

    missing = expected - found
    extra = found - expected
    for event, action in sorted(missing):
        print(f"{config_path} missing {event} -> {action} wiring")
        status = 1
    for event, action in sorted(extra):
        print(f"{config_path} has unexpected {event} -> {action} wiring")
        status = 1

sys.exit(status)
PY
)
if [ $? -ne 0 ]; then
  while IFS= read -r line; do
    [ -n "$line" ] && fail "$line"
  done <<< "$HOOK_CHECK"
fi

# --- 3. every repo shell script passes bash -n -----------------------------
while IFS= read -r -d '' f; do
  if ! ERR=$(bash -n "$f" 2>&1); then
    fail "$f failed bash -n: $ERR"
  fi
done < <(find scripts -type f -name '*.sh' -print0)

# --- 4. pointer integrity across CLAUDE.md / orchestration / matrix --------
POINTER_CHECK=$(python3 - <<'PY'
import re, os, subprocess, sys

FILES = ["CLAUDE.md", "AGENTS.md", "docs/orchestration.md", "docs/verification-matrix.md"]
BACKTICK_RE = re.compile(r'`([^`]+)`')
CANDIDATE_RE = re.compile(
    r'^(docs|scripts|tasks|backend|\.claude|\.codex|\.agents|\.github)/'
    r'.+\.(md|sh|py|yml|cs|csproj|json|txt)$'
)


def is_gitignored(path):
    # Paths matched by .gitignore are machine-local by design (e.g.
    # .claude/settings.local.json) — absent in a fresh CI checkout without
    # being a drift signal, so the existence requirement does not apply.
    # git check-ignore exits 0 when the path is ignored.
    return subprocess.run(
        ["git", "check-ignore", "-q", path],
        capture_output=True,
    ).returncode == 0


status = 0
for fn in FILES:
    if not os.path.isfile(fn):
        print(f"pointer source file missing: {fn}")
        status = 1
        continue
    with open(fn, encoding="utf-8") as f:
        lines = f.readlines()
    for i, line in enumerate(lines, 1):
        if "machinery-check:ignore" in line:
            continue
        for m in BACKTICK_RE.finditer(line):
            p = m.group(1)
            if "*" in p:
                continue
            if CANDIDATE_RE.match(p) and not os.path.exists(p) and not is_gitignored(p):
                print(f"{fn}:{i}: dangling pointer to missing file: {p}")
                status = 1

sys.exit(status)
PY
)
if [ $? -ne 0 ]; then
  while IFS= read -r line; do
    [ -n "$line" ] && fail "$line"
  done <<< "$POINTER_CHECK"
fi

# --- 5. tracked Claude project skills are exposed to Codex by symlink ------
SKILL_CHECK=$(python3 - <<'PY'
import os
import subprocess
import sys

result = subprocess.run(
    ["git", "ls-files", ".claude/skills/*/SKILL.md"],
    capture_output=True,
    text=True,
    check=True,
)

status = 0
for skill_file in sorted(line for line in result.stdout.splitlines() if line):
    source = os.path.dirname(skill_file)
    name = os.path.basename(source)
    link = os.path.join(".agents", "skills", name)
    if not os.path.islink(link):
        print(f"Codex skill link missing or not a symlink: {link}")
        status = 1
        continue
    if os.path.realpath(link) != os.path.realpath(source):
        print(f"Codex skill link points to wrong target: {link} -> {os.readlink(link)}")
        status = 1

sys.exit(status)
PY
)
if [ $? -ne 0 ]; then
  while IFS= read -r line; do
    [ -n "$line" ] && fail "$line"
  done <<< "$SKILL_CHECK"
fi

if [ "$status" -eq 0 ]; then
  echo "[machinery-check] ✓ harness wiring/hooks/skills/pointers all consistent"
fi
exit "$status"
