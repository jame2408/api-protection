#!/usr/bin/env bash
# source-lint.sh — cheap, mechanically-checkable bans that reflection / NetArchTest /
# the compiler cannot see on their own: C# syntax living in method bodies (constructor
# calls, parameter names) rather than the type graph, plus two repo-hygiene checks
# (MSBuild XML validity, this repo's scripts staying bash-3.2 compatible). Part of the
# ci-checks.sh gate (runs in both fast and full modes).
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

# Rule (exceptions.rule.md §E): failure codes are referenced via *FailureCodes constants,
# never as a bare string literal — `CreateFailure("FOO")` should be `CreateFailure(XFailureCodes.Foo)`.
bare_code=$(grep -rnE 'CreateFailure\("' "$SRC" --include='*.cs' \
    | grep -v '/obj/' || true)
if [[ -n "$bare_code" ]]; then
    echo "[source-lint] bare-string failure code — use a *FailureCodes constant:" >&2
    echo "$bare_code" | sed 's/^/  /' >&2
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

# Rule (tasks/lessons/20260704-msbuild-xml-comment-double-dash-nu1015.md): every
# MSBuild props/targets file must be well-formed XML — an XML
# comment containing `--` silently invalidates the whole file (NU1015) instead of failing
# loudly, so validity is checked directly rather than trusting `dotnet restore` to complain.
bad_xml=""
while IFS= read -r f; do
    full="$REPO_ROOT/$f"
    if ! python3 -c 'import sys, xml.dom.minidom; xml.dom.minidom.parse(sys.argv[1])' "$full" >/dev/null 2>&1; then
        bad_xml="${bad_xml}${full}"$'\n'
    fi
done < <(git -C "$REPO_ROOT" ls-files '*.props' '*.targets')
if [[ -n "$bad_xml" ]]; then
    echo "[source-lint] invalid MSBuild XML — a comment containing '--' would make MSBuild silently skip the whole file (NU1015):" >&2
    echo "$bad_xml" | sed 's/^/  /' >&2
    status=1
fi

# Rule (tasks/lessons/20260705-bash-3-2-compat.md): repo scripts must stay
# bash 3.2 compatible — `mapfile`/`readarray` are bash 4+ builtins, and `trap ... RETURN`
# does not fire when a function aborts under `set -e`. Excludes this file itself, whose
# rule text below is a literal self-match, not a violation.
bash_compat=$(grep -rnE '\bmapfile\b|\breadarray\b|trap .* RETURN' "$REPO_ROOT/scripts" "$REPO_ROOT/.claude/hooks" --include='*.sh' \
    | grep -v '/source-lint\.sh:' || true)
if [[ -n "$bash_compat" ]]; then
    echo "[source-lint] bash 3.2 incompatible construct (mapfile / readarray / trap ... RETURN):" >&2
    echo "$bash_compat" | sed 's/^/  /' >&2
    status=1
fi

# Rule (.claude/references/dotnet/di.rule.md §C): Scoped services (Handler/Service/Repository)
# must have dependencies injected directly, never build a child scope via
# IServiceScopeFactory.CreateScope() — that pattern is reserved for Singleton Middleware
# avoiding captive dependencies (di.rule.md §C's CookieValidationMiddleware example).
# *Middleware.cs and Program.cs are exempt as the legitimate call sites.
bad_scope=$(grep -rnE 'IServiceScopeFactory|\.CreateScope\(' "$SRC" --include='*.cs' \
    | grep -v '/obj/' | grep -vE '/[^/]*Middleware\.cs:' | grep -vE '/Program\.cs:' || true)
if [[ -n "$bad_scope" ]]; then
    echo "[source-lint] forbidden IServiceScopeFactory / .CreateScope( outside *Middleware.cs / Program.cs — Scoped services inject dependencies directly (di.rule.md §C):" >&2
    echo "$bad_scope" | sed 's/^/  /' >&2
    status=1
fi

if [[ $status -eq 0 ]]; then
    echo "[source-lint] ✓ no forbidden patterns"
fi
exit $status
