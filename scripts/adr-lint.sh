#!/usr/bin/env bash
# adr-lint.sh — structural lint for docs/adr/adr-*.md
#
# Checks (per ADR file):
#   1. Status line:                  "Accepted (YYYY-MM-DD)"
#   2. Required sections (## headings present):
#        Status / Context / Decision / Rationale /
#        Consequences / Alternatives Considered / Implementation Rules
#   3. Last numbered item in Implementation Rules is the governance clause
#        ("任何提案修改 1–N，必須先開新 ADR")
#   4. No file:line refs (\.(md|cs|csproj|feature):\d+)
#   5. Filename numbering is unique and sequential (no gaps, no dupes)
#   6. Each "### Alternative " block contains literal "Rejected."
#   7. Each "Negative" / "Trade-offs" bullet has a "Mitigation:" follow-up
#
# Usage:
#   scripts/adr-lint.sh                    # lint all docs/adr/adr-*.md
#   scripts/adr-lint.sh path/to/adr.md     # lint specific files
#
# Exit code:
#   0 — all checks pass
#   1 — at least one violation
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ADR_DIR="$REPO_ROOT/docs/adr"

violations=0
checked=0

red()    { printf '\033[31m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }
green()  { printf '\033[32m%s\033[0m\n' "$*"; }

report() {
    # report <file> <line-or-dash> <message>
    red "  ✗ $1${2:+:$2} — $3"
    violations=$((violations + 1))
}

check_file() {
    local f="$1"
    local rel="${f#"$REPO_ROOT"/}"
    checked=$((checked + 1))

    # 1. Status line
    if ! grep -qE '^Accepted \([0-9]{4}-[0-9]{2}-[0-9]{2}\)' "$f"; then
        report "$rel" - 'missing or malformed Status line — expected "Accepted (YYYY-MM-DD)"'
    fi

    # 2. Required sections
    local section
    for section in 'Status' 'Context' 'Decision' 'Rationale' \
                   'Consequences' 'Alternatives Considered' 'Implementation Rules'; do
        if ! grep -qE "^## ${section}\$" "$f"; then
            report "$rel" - "missing required section: ## ${section}"
        fi
    done

    # 3. Governance clause as last numbered item in Implementation Rules
    if grep -qE '^## Implementation Rules$' "$f"; then
        # extract Implementation Rules block, find last numbered line
        local last_rule
        last_rule=$(awk '
            /^## Implementation Rules$/ { in_block = 1; next }
            in_block && /^## / { in_block = 0 }
            in_block && /^[0-9]+\./ { last = $0 }
            END { print last }
        ' "$f")
        if ! printf '%s' "$last_rule" | grep -qE '任何提案修改.*必須先開新 ADR'; then
            report "$rel" - 'last Implementation Rule is not the governance clause ("任何提案修改 1–N，必須先開新 ADR")'
        fi
    fi

    # 4. No file:line refs
    local linenoref
    while IFS=: read -r ln rest; do
        # skip code fences and inline code that legitimately reference line
        # pragmatic: report any match — fence-aware logic adds complexity for low ROI
        report "$rel" "$ln" "file:line reference found — use stable anchor instead: $(printf '%s' "$rest" | sed 's/^[[:space:]]*//')"
    done < <(grep -nE '\.(md|cs|csproj|feature):[0-9]+' "$f" || true)

    # 6. Each "### Alternative " block contains "Rejected."
    awk '
        /^### Alternative / { name = $0; has_rejected = 0; next }
        /^### / && name { if (!has_rejected) print NR": "name; name = "" }
        /^## / && name  { if (!has_rejected) print NR": "name; name = "" }
        /Rejected\./ && name { has_rejected = 1 }
        END { if (name && !has_rejected) print NR": "name }
    ' "$f" | while IFS=: read -r ln name; do
        report "$rel" "$ln" "Alternative without explicit \"Rejected.\" marker — $name"
    done

    # 7. Trade-off bullets need Mitigation follow-up
    # Heuristic: inside "### Negative" or "### Negative / Trade-offs" block,
    # every top-level bullet ("- " at column 0) must be followed (within next 3 lines)
    # by a "Mitigation:" indented bullet.
    awk '
        /^### Negative/        { in_block = 1; next }
        /^### / && in_block    { in_block = 0 }
        /^## /  && in_block    { in_block = 0 }
        in_block && /^- /      {
            # capture line number of this bullet
            bullet_line = NR
            bullet_text = $0
            # peek next up to 5 lines for Mitigation
            found = 0
            for (i = 1; i <= 5; i++) {
                line = ""
                if ((getline line) > 0) {
                    NR_after = NR
                    if (line ~ /^- /) break
                    if (line ~ /Mitigation:/) { found = 1; break }
                } else break
            }
            if (!found) print bullet_line": "bullet_text
        }
    ' "$f" | while IFS=: read -r ln text; do
        report "$rel" "$ln" "Trade-off bullet without Mitigation follow-up — $text"
    done
}

check_filename_numbering() {
    # 5. Filename numbering: unique, sequential, no gaps
    local files=()
    local f
    for f in "$ADR_DIR"/adr-*.md; do
        [[ -e "$f" ]] || continue
        files+=("$f")
    done
    if [[ ${#files[@]} -eq 0 ]]; then
        return
    fi
    local prev=0
    local seen=" "
    local n
    for f in "${files[@]}"; do
        n=$(basename "$f" | sed -nE 's/^adr-([0-9]+)-.*\.md$/\1/p')
        if [[ -z "$n" ]]; then
            report "${f#"$REPO_ROOT"/}" - 'filename does not match adr-NNN-kebab-case.md pattern'
            continue
        fi
        n=$((10#$n))  # force base-10
        # dupe check (string-based — bash 3.2 compatible)
        if [[ "$seen" == *" $n "* ]]; then
            report "${f#"$REPO_ROOT"/}" - "duplicate ADR number: $n"
        fi
        seen="$seen$n "
        # gap check
        if [[ $((prev + 1)) -ne $n ]] && [[ $prev -ne 0 ]]; then
            report "${f#"$REPO_ROOT"/}" - "gap in ADR numbering: expected $((prev + 1)), got $n"
        fi
        prev=$n
    done
}

main() {
    local targets=()
    if [[ $# -eq 0 ]]; then
        local f
        for f in "$ADR_DIR"/adr-*.md; do
            [[ -e "$f" ]] || continue
            targets+=("$f")
        done
    else
        targets=("$@")
    fi

    if [[ ${#targets[@]} -eq 0 ]]; then
        yellow 'adr-lint: no ADR files to check.'
        exit 0
    fi

    local f
    for f in "${targets[@]}"; do
        check_file "$f"
    done

    # Filename-level checks (only when linting full set)
    if [[ $# -eq 0 ]]; then
        check_filename_numbering
    fi

    if [[ $violations -eq 0 ]]; then
        green "adr-lint: $checked file(s) passed."
        exit 0
    else
        red "adr-lint: $violations violation(s) across $checked file(s)."
        exit 1
    fi
}

main "$@"
