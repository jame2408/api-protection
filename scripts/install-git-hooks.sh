#!/usr/bin/env bash
# install-git-hooks.sh — install local git hooks for this repo.
#
# Hooks are not tracked by git directly; this script wires them in by setting
# core.hooksPath to scripts/git-hooks/. Run once per clone.
#
# Usage:
#   scripts/install-git-hooks.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOOKS_DIR="$REPO_ROOT/scripts/git-hooks"

if [[ ! -d "$HOOKS_DIR" ]]; then
    echo "error: $HOOKS_DIR does not exist" >&2
    exit 1
fi

git -C "$REPO_ROOT" config core.hooksPath scripts/git-hooks
chmod +x "$HOOKS_DIR"/*

echo "git hooks installed (core.hooksPath = scripts/git-hooks/)."
echo "active hooks:"
ls -1 "$HOOKS_DIR" | sed 's/^/  - /'
