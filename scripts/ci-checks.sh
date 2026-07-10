#!/usr/bin/env bash
# ci-checks.sh — the single source of truth for this repo's checks.
#
# Two modes, one script (so the fast subset can never drift from the full gate):
#   full  → 7 cheap checks (format, adr-lint, hook-smoke, zh-lint, source-lint,
#           machinery-check, bdd-lint) + restore/build/test (pre-push hook AND CI)
#   fast  → the same 7 cheap checks only, no build/test    (pre-commit hook)
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
    echo "[ci-checks] hook smoke test (shared Claude/Codex parity)"
    bash "$REPO_ROOT/scripts/hook-smoke.sh"
}

zh_lint() {
    echo "[ci-checks] zh lint (no Simplified Chinese in tracked files)"
    bash "$REPO_ROOT/scripts/zh-lint.sh"
}

source_lint() {
    echo "[ci-checks] source lint (C# bans / msbuild xml / bash 3.2 compat)"
    bash "$REPO_ROOT/scripts/source-lint.sh"
}

machinery_check() {
    echo "[ci-checks] machinery check (settings/hooks wiring + doc pointer integrity)"
    bash "$REPO_ROOT/scripts/machinery-check.sh"
}

bdd_lint() {
    echo "[ci-checks] bdd lint (progress ledger consistency)"
    bash "$REPO_ROOT/scripts/bdd-lint.sh"
}

build_and_test() {
    echo "[ci-checks] restore"
    dotnet restore "$SOLUTION"
    echo "[ci-checks] build (Release)"
    dotnet build "$SOLUTION" --no-restore -c Release
    echo "[ci-checks] test (unit + architecture + BDD functional)"
    local coverage_dir="${TMPDIR:-/tmp}/ci-checks-coverage"
    rm -rf "$coverage_dir"
    mkdir -p "$coverage_dir"
    dotnet test "$SOLUTION" --no-build -c Release \
        --collect:"XPlat Code Coverage" --results-directory "$coverage_dir"
    echo "[ci-checks] Handler coverage gate (docs/adr/adr-014-handler-coverage-gate.md)"
    bash "$REPO_ROOT/scripts/coverage-check.sh" "$coverage_dir"
}

case "$MODE" in
    fast)
        # Cheap, Docker-free early warning for every commit.
        format_check
        adr_lint
        hook_smoke
        zh_lint
        source_lint
        machinery_check
        bdd_lint
        ;;
    full)
        # Complete gate — identical to CI. Cheap checks first so a format/adr/source
        # error fails in seconds instead of after a full build+test.
        format_check
        adr_lint
        hook_smoke
        zh_lint
        source_lint
        machinery_check
        bdd_lint
        build_and_test
        ;;
    *)
        echo "usage: ci-checks.sh [fast|full]" >&2
        exit 2
        ;;
esac

echo "[ci-checks] ✓ ${MODE} checks passed"
