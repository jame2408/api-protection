---
name: code-review
description: |
  Technical code review and security audit workflow with two modes:
  PR Mode (reviews GitHub PRs via gh CLI with impact analysis) and
  Self Mode (reviews local uncommitted changes via git diff).
  Identifies security vulnerabilities, detects bugs, and analyzes dependency impact across the codebase.
  Use when: user says "review", "code review", "PR review", "check my code", "audit",
  "find bugs", "security", "vulnerability scan", "幫我看", "檢查", "有問題嗎",
  or uses /code-review command.
metadata:
  version: "1.1.0"
  trigger: /code-review, /review, or "review" keywords
---

# Code Review Workflow

Act as a **Senior Technical Reviewer** providing actionable feedback on security vulnerabilities, bugs, and dependency impact.

## Phase 0: Resolve Environment (MANDATORY)

Before any review:
1. Read [environment-setup.ref.md](environment-setup.ref.md)
2. Resolve `{CONFIG_ROOT}`, `{SKILL_DIR}`, and runtime/shell references
3. Use the resolved paths in all later phases

---

## Mode Detection

Confirm GitHub remote via `git remote get-url origin` before PR Mode. If remote is not GitHub, fall back to Self Mode or ask the user to confirm. See `{CONFIG_ROOT}/references/vcs/vcs-platform-commands.ref.md` for `gh` commands.

**Step 1: Trigger Detection**
- IF request contains review keywords (review, 審查, 檢查, 幫我看, bug, 問題, security, 漏洞, /code-review, /review) → Trigger
- IF request asks to optimize/refactor/improve code quality → Do not trigger; use coding-style instead
- ELSE → Do not trigger this skill

**Step 2: Mode Detection (PR vs Self)**
- IF request contains GitHub URL (`pull/(\d+)`) or PR reference (`PR\s*#?\s*(\d+)`) or `/code-review <number>` → **PR Mode**
- ELSE → **Self Mode**

**Step 3A: PR Mode**
1. Run `gh pr view <ID> --json isDraft,state,title,body,files` and `gh pr diff <ID>`
2. IF `isDraft` is true, state is not reviewable, or title indicates WIP → STOP
3. Run `gh pr checkout <ID>` to sync workspace → Go to Phase 1

**Step 3B: Self Mode**
1. Run `git --no-pager diff --stat HEAD` and `git --no-pager diff HEAD`
2. IF no changes → STOP, report "Nothing to review"
3. Go to Phase 1

---

## Review Philosophy

1. **Be a Technical Consultant, not a Process Robot** — Focus on catching real bugs, not formatting
2. **Verify Before Criticizing** — Check usage context before suggesting pattern changes
3. **Prioritize by Impact** — Security > Bugs > Performance > Style
4. **Respect Developer Intent** — Understand the "why" before questioning the "how"

---

## Phase 1: Triage (30 seconds)

| Change Size | Strategy |
|-------------|----------|
| **Small (<100 lines)** | Line-by-line, focus on logic correctness |
| **Medium (100-500 lines)** | Review by file, focus on integration points |
| **Large (>500 lines)** | Architecture first, then critical paths only |

**Skip if**: draft/WIP, automated (dependabot/renovate), trivial (typo/comment), no changes, whitespace-only.

---

## Phase 2: Context Gathering

**PR Mode**: Read PR description → Identify changed components → Check project guidelines (`CONTRIBUTING.md`, `CLAUDE.md`) → Detect tech stack.

**Self Mode**: Run `git diff HEAD` → Identify changed files → Check recent commit context.

**Both modes — project must-read (mandatory)**: When the diff touches .NET / C# backend code (production or test) in this repo, load before Phase 3 analysis — regardless of mode or diff size:
- `CLAUDE.md` §0 Reference Loading — the authoritative must-read source; this skill only points to it and does not restate rule content.
- `docs/adr/` — every ADR whose `## Status` section reads `Accepted`. Check each `docs/adr/adr-*.md` file's own Status section; do not hardcode an ADR number list here, so this step never goes stale as ADRs are added.

---

## Phase 2.5: Impact & Dependency Analysis (MANDATORY)

> ⚠️ **Without this phase, no inference may be classified as a confirmed Bug.**

Before applying rules (Phase 3), verify the **impact surface**:

1. **Extract key entities** from diff: changed types, constants, enums, public method signatures.
2. **Search the full project** for each entity using these tools in order:
   - **Exact search** (e.g. `grep -r "MethodName" src/`) — find all callers, definitions, references
   - **Read files** — open definition files to verify inheritance, DTO fields, constant values (never infer from diff alone)
   - **Semantic search** (optional) — only when exact name is unknown
3. **Classify conclusions**:
   - ✅ Verified against actual files → may classify as Bug
   - ❌ Unverified (diff-only inference) → Suggestion / Unconfirmed only

See [impact-analysis.ref.md](impact-analysis.ref.md) for detailed procedures and worked examples.

---

## Phase 3: Load Rules & Analyze

**Step 1: Load review rules** based on detected tech stack. Scan `{CONFIG_ROOT}/references/` directories:
- Load `general/*.rule.md` first, then stack-specific `{stack}/*.rule.md` (specific overrides general)
- Only auto-load `*.rule.md` files. Skip `*.guide.md`; skip `*.ref.md` unless this SKILL.md explicitly names it.
- Self-correction: If you see a new `*.rule.md`, read it. If you see `*.guide.md` or an unmentioned `*.ref.md`, do not auto-load it.
- Skip-if-missing: if a detected stack has no matching directory in this repo (e.g. only `dotnet/` and `general/` exist; there is no `nodejs/` or `python/`), treat it as "no reference docs for that stack yet" and skip it — not an error, does not block the review.

**Step 2: Execute analysis** using loaded rules as checklist:
1. Scan `git diff` content
2. Match patterns from rule files (e.g., "Async Deadlock", "N+1 Query")
3. Ignore issues not in rules unless they are obvious logical errors
4. Categorize: Security (Must Fix) > Bugs (Must Fix) > Performance (Should Fix)

**Step 3: Verify context** — Apply Phase 2.5 verification before reporting. Insufficient context → Suggestion, not Bug.

---

## Phase 4: Report

**Confidence scoring**: 80-100 = certain (primary finding), 50-79 = likely (Suggestions section), <50 = do not report.

**Evidence**: Attach detailed evidence only for high-risk issues (security, public API changes, shared constants). Keep other findings concise.

**Templates**: Read `{SKILL_DIR}/templates/report-pr-mode.md` or `{SKILL_DIR}/templates/report-self-mode.md` before generating output.

**Output**:
- PR Mode → Output to chat. If user requests, post to PR via `{CONFIG_ROOT}/references/vcs/code-review-posting-github.ref.md`.
- Self Mode → Output to chat only. If no critical issues → ask if user wants to commit. If issues found → suggest fixing first.

---

## Anti-Patterns

❌ Nitpick formatting (leave to linters) · ❌ Suggest patterns without checking usage · ❌ Report unrelated issues outside the reviewed impact surface · ❌ Criticize code style · ❌ Recommend rewrites for working code
