#!/usr/bin/env bash
# ci-checks.sh — the single source of truth for this repo's checks.
#
# Two modes, one script (so the fast subset can never drift from the full gate):
#   full  → restore + build + test + format + adr-lint   (pre-push hook AND CI)
#   fast  → format + adr-lint only                        (pre-commit hook)
#
# Invariant: pre-push and CI both run `full`, so "passes pre-push" provably means
# "passes CI". `fast` is a quick pre-commit early-warning — a strict subset of the
# same commands, not a separate gate.
#
# Notes:
#   - Checks the working tree (what is on disk), not the staged index.
#   - `full` runs Testcontainers-based BDD tests, so it needs Docker running;
#     `fast` does not touch Docker.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"
SOLUTION="backend/ApiKeyManagement.slnx"
MODE="${1:-full}"

format_check() {
    echo "[ci-checks] format --verify-no-changes"
    dotnet format "$SOLUTION" --verify-no-changes
}

adr_lint() {
    echo "[ci-checks] adr structural lint"
    bash "$REPO_ROOT/scripts/adr-lint.sh"
}

hook_smoke() {
    echo "[ci-checks] hook smoke test (session-init.sh injection)"
    bash "$REPO_ROOT/scripts/hook-smoke.sh"
}

zh_lint() {
    echo "[ci-checks] zh lint (no Simplified Chinese in tracked files)"
    bash "$REPO_ROOT/scripts/zh-lint.sh"
}

source_lint() {
    echo "[ci-checks] source lint (new Failure / cancel naming)"
    bash "$REPO_ROOT/scripts/source-lint.sh"
}

build_and_test() {
    echo "[ci-checks] restore"
    dotnet restore "$SOLUTION"
    echo "[ci-checks] build (Release)"
    dotnet build "$SOLUTION" --no-restore -c Release
    echo "[ci-checks] test (unit + architecture + BDD functional)"
    dotnet test "$SOLUTION" --no-build -c Release
}

case "$MODE" in
    fast)
        # Cheap, Docker-free early warning for every commit.
        format_check
        adr_lint
        hook_smoke
        zh_lint
        source_lint
        ;;
    full)
        # Complete gate — identical to CI. Cheap checks first so a format/adr/source
        # error fails in seconds instead of after a full build+test.
        format_check
        adr_lint
        hook_smoke
        zh_lint
        source_lint
        build_and_test
        ;;
    *)
        echo "usage: ci-checks.sh [fast|full]" >&2
        exit 2
        ;;
esac

echo "[ci-checks] ✓ ${MODE} checks passed"
