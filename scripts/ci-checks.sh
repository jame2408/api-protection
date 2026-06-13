#!/usr/bin/env bash
# ci-checks.sh — the single source of truth for this repo's build/test gate.
#
# Run by BOTH:
#   - the local pre-commit hook  (scripts/git-hooks/pre-commit)
#   - CI                         (.github/workflows/ci.yml)
# so that "passes locally" provably means "passes CI": the two gates are the
# SAME script and cannot drift. Change a check here and both move together.
#
# Notes:
#   - Checks the working tree (what is on disk), not the staged index. Save
#     everything you intend to ship before committing.
#   - The test step uses Testcontainers (FunctionalTests) and therefore needs
#     Docker running. No Docker = the BDD scenarios fail, exactly as they would
#     on a CI runner without Docker.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"
SOLUTION="backend/ApiKeyManagement.slnx"

echo "[ci-checks] 1/5 restore"
dotnet restore "$SOLUTION"

echo "[ci-checks] 2/5 build (Release)"
dotnet build "$SOLUTION" --no-restore -c Release

echo "[ci-checks] 3/5 format --verify-no-changes"
dotnet format "$SOLUTION" --no-restore --verify-no-changes

echo "[ci-checks] 4/5 test (unit + architecture + BDD functional)"
dotnet test "$SOLUTION" --no-build -c Release

echo "[ci-checks] 5/5 adr structural lint"
bash "$REPO_ROOT/scripts/adr-lint.sh"

echo "[ci-checks] ✓ all checks passed"
