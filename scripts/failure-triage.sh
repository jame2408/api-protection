#!/usr/bin/env bash
# failure-triage.sh — report tool (tool × normalized-error-signature) counts from
# .claude/failures.jsonl so repeated failures surface without anyone re-reading the
# raw log by eye. Report tool, not a gate (docs/adr/adr-018-failure-triage-and-observations-retirement.md
# decision §2/§3): a signature seen >= 2 times is flagged REPEAT, which is the
# mechanical trigger for the phase-close triage obligation (ADR-018 decision §3) —
# every REPEAT must be dispositioned as lesson / todo / checkpoint "not converting
# because ..." before tasks/checkpoint.md is updated at phase close.
#
# Usage:
#   scripts/failure-triage.sh [jsonl-path]
#     jsonl-path defaults to <repo-root>/.claude/failures.jsonl; an explicit path
#     is accepted so fixtures can be triaged without touching the real log.
#
# Exit code: always 0 (missing/empty input is a valid, reportable state, not an
# error), except 2 for a usage error (more than one argument).
set -uo pipefail

if [ "$#" -gt 1 ]; then
    echo "Usage: $(basename "$0") [jsonl-path]" >&2
    exit 2
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
if [ -z "$REPO_ROOT" ]; then
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
fi

JSONL_PATH="${1:-$REPO_ROOT/.claude/failures.jsonl}"

# Standing lessons-triage trigger (tasks/todo.md「Lessons triage 常設觸發」): active
# lessons >= 15 means a lessons triage is due. The threshold evaluation lives here
# because this report is already mandatory before every phase-close checkpoint update
# (ADR-018 decision §3) — the clause previously existed only as prose and was crossed
# without firing (2026-07-10 loop-audit root cause). LESSONS_DIR is overridable so the
# below-threshold branch can be exercised against a fixture directory.
report_lessons_count() {
    local dir="${LESSONS_DIR:-$REPO_ROOT/tasks/lessons}"
    local threshold=15
    local count=0
    if [ -d "$dir" ]; then
        count=$(grep -l '^status: active' "$dir"/*.md 2>/dev/null | grep -cv '_README' || true)
    fi
    if [ "$count" -ge "$threshold" ]; then
        echo "[failure-triage] lessons: active=$count ≥ $threshold — lessons triage 到期（tasks/todo.md 常設觸發條款）"
    else
        echo "[failure-triage] lessons: active=${count}（< ${threshold}，未觸發）"
    fi
}

if [ ! -f "$JSONL_PATH" ] || [ ! -s "$JSONL_PATH" ]; then
    echo "[failure-triage] no records to triage: $JSONL_PATH (missing or empty)"
    report_lessons_count
    exit 0
fi

PY=$(cat <<'PYEOF'
import json, os, re, sys

path = os.environ["FAILURE_TRIAGE_PATH"]

EXIT_CODE_RE = re.compile(r'^Exit code \d+$')
DIGIT_RE = re.compile(r'\d+')
WS_RE = re.compile(r'\s+')


def signature(error):
    if not isinstance(error, str):
        error = str(error)
    lines = error.split("\n")
    kept = [l for l in lines if not EXIT_CODE_RE.match(l.strip())]
    nonempty = [l for l in kept if l.strip() != ""]
    if nonempty:
        raw = nonempty[-1]
    else:
        fallback = None
        for l in lines:
            if EXIT_CODE_RE.match(l.strip()):
                fallback = l.strip()
        raw = fallback if fallback else "Exit code N"
    raw = DIGIT_RE.sub("N", raw)
    raw = WS_RE.sub(" ", raw).strip()
    return raw[:120]


groups = {}
order = []
timestamps = []
total = 0
malformed = 0

with open(path, "r", encoding="utf-8") as f:
    for line in f:
        line = line.rstrip("\n")
        if line.strip() == "":
            continue
        try:
            record = json.loads(line)
        except Exception:
            malformed += 1
            continue
        if not isinstance(record, dict):
            malformed += 1
            continue
        total += 1
        tool = record.get("tool", "") or ""
        error = record.get("error", "") or ""
        ts = record.get("ts", "") or ""
        if ts:
            timestamps.append(ts)
        key = (tool, signature(error))
        if key not in groups:
            groups[key] = 0
            order.append(key)
        groups[key] += 1

if total == 0 and malformed == 0:
    print("[failure-triage] no records to triage: %s (missing or empty)" % path)
    sys.exit(0)

earliest = min(timestamps) if timestamps else "?"
latest = max(timestamps) if timestamps else "?"

print("[failure-triage] %d records (%d malformed skipped), %s .. %s" % (total, malformed, earliest, latest))

rows = sorted(order, key=lambda k: groups[k], reverse=True)
for key in rows:
    tool, sig = key
    count = groups[key]
    tag = "REPEAT" if count >= 2 else ""
    print("%4dx  %-8s %-8s %s" % (count, tag, tool, sig))
PYEOF
)

FAILURE_TRIAGE_PATH="$JSONL_PATH" python3 -c "$PY"
report_lessons_count
exit 0
