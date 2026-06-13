#!/usr/bin/env bash
# source-lint.sh — cheap syntax-level bans that reflection / NetArchTest cannot see,
# because they live in method bodies (constructor calls) or parameter names rather than
# in the type graph. Part of the ci-checks.sh gate (runs in both fast and full modes).
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
SRC="$REPO_ROOT/backend/src"
status=0

# Rule (CLAUDE.md / exceptions.rule.md §A): never `new Failure(...)` — construct via
# FailureProvider.CreateFailure(*FailureCodes.X). The sole legitimate construction is the
# factory itself, FailureProvider.cs.
new_failure=$(grep -rnE 'new Failure\(' "$SRC" --include='*.cs' \
    | grep -v '/obj/' | grep -v '/FailureProvider\.cs:' || true)
if [[ -n "$new_failure" ]]; then
    echo "[source-lint] forbidden 'new Failure(' — use FailureProvider.CreateFailure():" >&2
    echo "$new_failure" | sed 's/^/  /' >&2
    status=1
fi

# Rule (CLAUDE.md / naming.guide.md §B): CancellationToken parameters are named `cancel`,
# never `cancellationToken` or `ct`.
bad_cancel=$(grep -rnE 'CancellationToken (cancellationToken|ct)\b' "$SRC" --include='*.cs' \
    | grep -v '/obj/' || true)
if [[ -n "$bad_cancel" ]]; then
    echo "[source-lint] CancellationToken must be named 'cancel' (not cancellationToken / ct):" >&2
    echo "$bad_cancel" | sed 's/^/  /' >&2
    status=1
fi

if [[ $status -eq 0 ]]; then
    echo "[source-lint] ✓ no forbidden patterns"
fi
exit $status
