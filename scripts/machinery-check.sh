#!/usr/bin/env bash
# machinery-check.sh — self-check for the governance machinery itself
# (settings.json wiring, hook scripts, cross-doc pointer integrity).
#
# Adopted from zeuikli/claude-code-workspace's healthcheck.sh (see
# tasks/process-improvement-plan.md §10, P2) — rewritten fail-loud. That
# repo's own healthcheck used `if [ -f ]` to silently skip missing files,
# which is exactly the anti-pattern this check exists to prevent (see
# process-improvement-plan.md §10.1, last row). Every check below is either
# pass or hard-fail; nothing is silently skipped.
#
# Checks:
#   1. .claude/settings.json is valid JSON; .mcp.json too, if present.
#   2. Every hook script referenced in .claude/settings.json's hooks section
#      exists, is executable, and passes bash -n (.sh) / py_compile (.py).
#   3. Every .claude/hooks/*.sh and scripts/*.sh passes bash -n.
#   4. Pointer integrity: every backtick-wrapped path in CLAUDE.md,
#      docs/orchestration.md, docs/verification-matrix.md matching
#      ^(docs|scripts|tasks|backend|\.claude|\.github)/.+\.(md|sh|py|yml|cs|csproj|json|txt)$
#      must exist on disk. Glob-style entries (containing `*`) are skipped;
#      a line containing the literal marker "machinery-check:ignore" is
#      exempt (same convention as zh-lint:allow).
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

# --- 1. settings.json / .mcp.json JSON validity ----------------------------
if ! ERR=$(python3 -c "import json; json.load(open('.claude/settings.json'))" 2>&1); then
  fail ".claude/settings.json is not valid JSON: $ERR"
fi

# .mcp.json is optional tooling config (Tessl et al., see
# tasks/process-improvement-plan.md §9.3 D-2) — checking "if present" here is
# the spec's own conditional, not a silent skip of something required to exist.
if [ -f .mcp.json ]; then
  if ! ERR=$(python3 -c "import json; json.load(open('.mcp.json'))" 2>&1); then
    fail ".mcp.json is not valid JSON: $ERR"
  fi
fi

# --- 2. hook scripts referenced in .claude/settings.json's hooks section ---
HOOK_CHECK=$(python3 - <<'PY'
import json, os, re, subprocess, sys

with open(".claude/settings.json", encoding="utf-8") as f:
    settings = json.load(f)

cmd_re = re.compile(r'"([^"]+\.(?:sh|py))"')
paths = set()
for entries in (settings.get("hooks") or {}).values():
    for entry in entries:
        for h in entry.get("hooks", []):
            cmd = h.get("command", "")
            for m in cmd_re.finditer(cmd):
                p = m.group(1).replace("$CLAUDE_PROJECT_DIR/", "")
                paths.add(p)

status = 0
for p in sorted(paths):
    if not os.path.isfile(p):
        print(f"hook script referenced in .claude/settings.json does not exist: {p}")
        status = 1
        continue
    if not os.access(p, os.X_OK):
        print(f"hook script not executable: {p}")
        status = 1
    if p.endswith(".sh"):
        r = subprocess.run(["bash", "-n", p], capture_output=True, text=True)
    else:
        r = subprocess.run(["python3", "-m", "py_compile", p], capture_output=True, text=True)
    if r.returncode != 0:
        print(f"{p} failed syntax check: {r.stderr.strip()}")
        status = 1

sys.exit(status)
PY
)
if [ $? -ne 0 ]; then
  while IFS= read -r line; do
    [ -n "$line" ] && fail "$line"
  done <<< "$HOOK_CHECK"
fi

# --- 3. every .claude/hooks/*.sh and scripts/*.sh passes bash -n -----------
while IFS= read -r -d '' f; do
  if ! ERR=$(bash -n "$f" 2>&1); then
    fail "$f failed bash -n: $ERR"
  fi
done < <(find .claude/hooks scripts -maxdepth 1 -name '*.sh' -print0)

# --- 4. pointer integrity across CLAUDE.md / orchestration / matrix --------
POINTER_CHECK=$(python3 - <<'PY'
import re, os, sys

FILES = ["CLAUDE.md", "docs/orchestration.md", "docs/verification-matrix.md"]
BACKTICK_RE = re.compile(r'`([^`]+)`')
CANDIDATE_RE = re.compile(
    r'^(docs|scripts|tasks|backend|\.claude|\.github)/.+\.(md|sh|py|yml|cs|csproj|json|txt)$'
)

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
            if CANDIDATE_RE.match(p) and not os.path.exists(p):
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

if [ "$status" -eq 0 ]; then
  echo "[machinery-check] ✓ settings/hooks/pointers all consistent"
fi
exit "$status"
