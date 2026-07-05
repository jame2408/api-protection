#!/usr/bin/env bash
# mutation-test.sh — QA #2 on-demand mutation testing via Stryker.NET.
#
# Status: NOT a CI/pre-commit/pre-push gate. On-demand only, run manually by a
# developer or orchestrator when investigating test-suite strength for a
# specific Bounded Context. Approved by user 2026-07-05.
#
# Why test-project mode (not solution mode): the solution file
# (backend/ApiKeyManagement.slnx) is in the newer .slnx format, which Stryker's
# solution mode may not support. This script always drives Stryker from the
# FunctionalTests project (`--project <csproj>`); the BC under test must be a
# DIRECT ProjectReference of FunctionalTests (Stryker does not search transitive
# references — that is why the BC references were made explicit in the csproj).
#
# Usage:
#   bash scripts/mutation-test.sh <KeyLifecycle|TenantManagement> [extra stryker args...]
#
# Output: Stryker writes its own report directory (StrykerOutput/<timestamp>/)
# under backend/tests/FunctionalTests/. Open the `reports/mutation-report.html`
# file inside it for the HTML report. No mutation-score threshold is set here
# (no --break-at) — this is deliberately non-gating; thresholds are left at
# Stryker's defaults and are informational only.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"

BC="${1:-}"
if [ -z "$BC" ]; then
  echo "Usage: bash scripts/mutation-test.sh <KeyLifecycle|TenantManagement> [extra stryker args...]" >&2
  exit 1
fi
shift

case "$BC" in
  KeyLifecycle)
    PROJECT_FILE="ApiKeyManagement.KeyLifecycle.csproj"
    ;;
  TenantManagement)
    PROJECT_FILE="ApiKeyManagement.TenantManagement.csproj"
    ;;
  *)
    echo "Unknown target '$BC'. Expected: KeyLifecycle | TenantManagement" >&2
    exit 1
    ;;
esac

cd "$REPO_ROOT/backend/tests/FunctionalTests"

dotnet tool run dotnet-stryker -- \
  --project "$PROJECT_FILE" \
  --reporter html \
  --reporter cleartext \
  "$@"
