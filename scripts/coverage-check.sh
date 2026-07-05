#!/usr/bin/env bash
# coverage-check.sh — Handler coverage gate (docs/adr/adr-014-handler-coverage-gate.md).
#
# Usage: coverage-check.sh <coverage-results-dir> [threshold-override]
#
#   <coverage-results-dir>  Directory (searched recursively) containing one or more
#                            `coverage.cobertura.xml` reports produced by
#                            `dotnet test --collect:"XPlat Code Coverage"`.
#   [threshold-override]    Optional. Overrides the default threshold below.
#                            For intentional-red verification and tests ONLY —
#                            never use this to quietly relax the real gate.
#
# Judges every concrete `*Handler` class's line coverage against THRESHOLD:
#   - compiler-generated async state machines (cobertura class name
#     "Namespace.Class/<Method>d__N") are merged back into the parent class.
#   - multiple reports covering the same class are merged by per-line max hits.
#   - any single Handler class below threshold => FAIL, exit non-zero
#     (no aggregate/average — see ADR-014 Decision §2 rationale).
#   - no cobertura reports, or no Handler class found in them => fail-loud,
#     exit non-zero (never a silent pass).
#
# Authority for the 80% constant: CLAUDE.md §4 Verification Standards
# ("unit coverage ≥ 80% for Handler code") + docs/adr/adr-014-handler-coverage-gate.md.
set -euo pipefail

THRESHOLD=80

RESULTS_DIR="${1:-}"
THRESHOLD_OVERRIDE="${2:-}"

if [[ -z "$RESULTS_DIR" ]]; then
    echo "usage: coverage-check.sh <coverage-results-dir> [threshold-override]" >&2
    exit 2
fi

if [[ -n "$THRESHOLD_OVERRIDE" ]]; then
    THRESHOLD="$THRESHOLD_OVERRIDE"
fi

if [[ ! -d "$RESULTS_DIR" ]]; then
    echo "[coverage-check] FAIL coverage results directory not found: $RESULTS_DIR" >&2
    exit 1
fi

REPORTS=()
while IFS= read -r -d '' report; do
    REPORTS+=("$report")
done < <(find "$RESULTS_DIR" -type f -name "coverage.cobertura.xml" -print0)

if [[ "${#REPORTS[@]}" -eq 0 ]]; then
    echo "[coverage-check] FAIL no coverage.cobertura.xml reports found under: $RESULTS_DIR" >&2
    exit 1
fi

python3 - "$THRESHOLD" "${REPORTS[@]}" <<'PYEOF'
import sys
from xml.etree import ElementTree as ET

threshold = float(sys.argv[1])
files = sys.argv[2:]

# base class full name -> { line number: max hits seen across all reports }
classes = {}

for path in files:
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        print(f"[coverage-check] FAIL malformed cobertura report {path}: {exc}", file=sys.stderr)
        sys.exit(1)

    for cls in root.iter("class"):
        name = cls.get("name", "")
        # Compiler-generated async state machines report as
        # "Namespace.Outer/<Method>d__N" — merge back into the parent class.
        base_name = name.split("/", 1)[0]
        lines_el = cls.find("lines")
        if lines_el is None:
            continue
        bucket = classes.setdefault(base_name, {})
        for line in lines_el.findall("line"):
            number = line.get("number")
            hits = int(line.get("hits", "0"))
            if number not in bucket or hits > bucket[number]:
                bucket[number] = hits

handlers = {name: lines for name, lines in classes.items() if name.endswith("Handler")}

if not handlers:
    print("[coverage-check] FAIL no *Handler classes found in coverage reports", file=sys.stderr)
    sys.exit(1)

failed = False
for name in sorted(handlers):
    lines = handlers[name]
    total = len(lines)
    covered = sum(1 for hits in lines.values() if hits > 0)
    pct = (covered / total * 100.0) if total else 0.0
    if pct < threshold:
        print(f"[coverage-check] FAIL {name}: {pct:.1f}% < {threshold:.0f}%")
        failed = True
    else:
        print(f"[coverage-check] OK {name}: {pct:.1f}%")

sys.exit(1 if failed else 0)
PYEOF
