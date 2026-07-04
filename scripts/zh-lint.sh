#!/usr/bin/env bash
# zh-lint.sh — block Simplified Chinese characters in tracked files (ADR-009).
#
# Char table: scripts/data/opencc-STCharacters.txt, vendored verbatim from
# OpenCC (Apache-2.0). A character is treated as simplified-only when it
# appears as an STCharacters key and is NOT among its own traditional
# mappings — e.g. 干→乾/幹/干 maps to itself so 干 stays legal, while a key
# that only maps to a different traditional form is flagged.
#
# Exemption: any line containing the literal marker "zh-lint:allow" is
# skipped — used for deliberate quotations (e.g. a lesson documenting a
# violation incident). The marker must sit on the same line as the quote.
#
# Exit code: 0 — clean; 1 — violations printed as path:line: chars | snippet.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$REPO_ROOT"

python3 - <<'PY'
import subprocess, sys

TABLE = "scripts/data/opencc-STCharacters.txt"
SELF_EXEMPT = {TABLE}  # the table itself is full of simplified chars

# OpenCC treats these as convertible, but they are standard/common Traditional
# forms in Taiwan usage (群→羣, 秘→祕 are variant preferences, not simplified
# spellings). Extending this set is a rule change — go through an ADR
# (see ADR-009).
ACCEPTED_VARIANTS = {"群", "秘"}

flagged = set()
with open(TABLE, encoding="utf-8") as f:
    for line in f:
        if line.startswith("#") or "\t" not in line:
            continue
        key, targets = line.rstrip("\n").split("\t", 1)
        if key not in targets.split(" ") and key not in ACCEPTED_VARIANTS:
            flagged.add(key)

files = subprocess.run(
    ["git", "ls-files"], capture_output=True, text=True, check=True
).stdout.splitlines()

violations = 0
for path in files:
    if path in SELF_EXEMPT:
        continue
    try:
        with open(path, "rb") as f:
            raw = f.read()
    except OSError:
        continue
    if b"\x00" in raw[:8192]:  # skip binary
        continue
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        continue
    for lineno, line in enumerate(text.splitlines(), 1):
        if "zh-lint:allow" in line:
            continue
        hits = [c for c in line if c in flagged]
        if hits:
            violations += 1
            snippet = line.strip()[:60]
            print(f"{path}:{lineno}: {''.join(sorted(set(hits)))} | {snippet}")

if violations:
    print(f"[zh-lint] ✗ {violations} line(s) contain Simplified Chinese "
          f"(quote deliberately with a same-line 'zh-lint:allow' marker)",
          file=sys.stderr)
    sys.exit(1)
print("[zh-lint] ✓ no simplified characters")
PY
