#!/usr/bin/env bash
# bdd-lint.sh — BDD progress ledger consistency check (no state, working tree only).
#
# Recomputes the scenario/@ignore counts directly from the .feature files and
# asserts they match the ledger line in tasks/bdd-progress.md
# ("**已通過：** N / M"). Catches the ledger silently drifting from reality
# (e.g. a scenario added/removed, or the passed-count edited without actually
# moving an @ignore tag). Part of the ci-checks.sh gate (fast and full).
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
FEATURES_DIR="$REPO_ROOT/backend/tests/FunctionalTests/Features"
LEDGER="$REPO_ROOT/tasks/bdd-progress.md"

total=0
ignored=0
while IFS= read -r f; do
    c=$(grep -c '^[[:space:]]*Scenario' "$f" || true)
    i=$(grep -c '@ignore' "$f" || true)
    total=$((total + c))
    ignored=$((ignored + i))
done < <(git -C "$REPO_ROOT" ls-files "$FEATURES_DIR/**/*.feature")

ledger_line=$(grep -E '^\*\*已通過：\*\* [0-9]+ / [0-9]+' "$LEDGER" || true)
if [[ -z "$ledger_line" ]]; then
    echo "[bdd-lint] FAIL could not find ledger line '**已通過：** N / M' in $LEDGER" >&2
    echo "           (missing, or its format changed — update this script or the ledger)" >&2
    exit 1
fi

passed=$(echo "$ledger_line" | sed -E 's/^\*\*已通過：\*\* ([0-9]+) \/ ([0-9]+).*/\1/')
declared_total=$(echo "$ledger_line" | sed -E 's/^\*\*已通過：\*\* ([0-9]+) \/ ([0-9]+).*/\2/')

expected_passed=$((total - ignored))
status=0

if [[ "$declared_total" -ne "$total" ]]; then
    echo "[bdd-lint] FAIL ledger total ($declared_total) != actual scenario count ($total)" >&2
    status=1
fi

if [[ "$passed" -ne "$expected_passed" ]]; then
    echo "[bdd-lint] FAIL ledger passed ($passed) != total - ignored ($total - $ignored = $expected_passed)" >&2
    status=1
fi

if [[ $status -ne 0 ]]; then
    echo "[bdd-lint] ledger says: $passed / $declared_total ; actual: total=$total ignored=$ignored expected_passed=$expected_passed" >&2
    echo "[bdd-lint] fix: update tasks/bdd-progress.md's '**已通過：** N / M' line to match reality" >&2
    exit 1
fi

echo "[bdd-lint] ✓ progress ledger consistent (${passed}/${declared_total} passed, ignored ${ignored}, total ${total})"
