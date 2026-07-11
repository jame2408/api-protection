# Claude Code Rules & Workflow

> 內容分級原則（`docs/adr/adr-013-content-tiering-and-injection-slimming.md`）：本檔只保留「全局且每 session 需要」的內容；細則一律指針化到權威文件（rule.md / Accepted ADR / `docs/verification-matrix.md`）。

## Commands

```bash
# Build
dotnet build backend/ApiKeyManagement.slnx
# Run (development)
dotnet run --project backend/src/Host/ApiKeyManagement.Api.csproj
# Run all tests
dotnet test backend/ApiKeyManagement.slnx
# Run BDD functional tests only
dotnet test backend/tests/FunctionalTests/
# Run architecture tests
dotnet test backend/tests/Architecture.Tests/
```

## Working Agreement

_How Claude and the user collaborate — committed to the repo so it persists across machines._

### Orchestrator Brief

本專案真正目的是建立「多模型統一開發 loop」，key 管理服務只是載體。接手本 repo 的模型即擔任**協調者** — 角色路由、executor 義務、停止條件、token 紀律全在 `docs/orchestration.md`（唯一權威，此處不複寫）。開工第一動作：讀 `tasks/checkpoint.md` 接手，勿要求使用者重述背景。

### Autonomy Scope
- **Bug reports**: Resolve autonomously — analyze logs, isolate root cause, fix, verify.
- **Feature tasks**: Document the plan in `tasks/todo.md` and get user approval before starting.
- **Business logic & domain decisions**: Always stop and ask — never assume intent.

### Change Discipline
- Prefer small, reviewable changes; align new files with existing repo patterns, not generic templates.
- Never bulk-rewrite `CLAUDE.md`; changes must be scoped, intentional, user-initiated.
- Verify the exact file path before editing — do not assume based on name/type similarity.
- `.claude/settings.json` = shared defaults, `.claude/settings.local.json` = machine-local; don't modify either unless explicitly requested.
- Evaluate all suggestions critically; never transcribe user text verbatim — compress and clarify. Say so directly if a suggestion contradicts existing rules.

### Architecture Decision Records (ADR)
- New ADRs MUST start from `docs/adr/_template.md` (encodes required structure + governance clause); file naming `docs/adr/adr-NNN-kebab-case-title.md`. <!-- machinery-check:ignore: placeholder pattern, not a real file -->
- Reference other docs by stable anchors (heading / symbol / quote) — never `file:line`. Final Implementation Rule MUST be the governance clause ("任何提案修改 1–N，必須先開新 ADR").
- ADRs touching reference docs / CLAUDE.md / examples MUST list "同步項目" (Status one-liner or Decision sub-section) and land all sync edits in the same commit.

#### Validation
- **結構性 lint**：`scripts/adr-lint.sh`（Status 格式／必要章節／governance clause／禁 file:line／檔名編號／Alternatives 需 "Rejected."／Trade-off 需 "Mitigation:"）；staged 含 `docs/adr/` 時 pre-commit 自動跑。
- **驗收指令**：承諾 repo 範圍清理的 ADR，Implementation Rules 須內嵌可執行驗證指令（範例見 ADR-006 §6）。
- **Review checklist（判斷型，7 項）**：合併 ADR PR 前逐條核對，清單見 `docs/adr/_template.md` 內建的 Review Checklist 註解區。

## Workflow Orchestration

### 0. Reference Loading
Before writing any backend code (production or test) this session, read `.claude/references/{dotnet,general}/*.rule.md` if not yet loaded. Once per session.

### 1. Plan-First Approach
Enter Plan Mode when: 3+ interdependent files; architectural decisions (new BC slice, dependency, schema/contract change); a wrong approach risks major rework; a prior attempt failed. Skip when: single-file change with a clear solution; hotfix/typo/rename; fully reversible with no downstream impact.
- **Pivot**: on deviation or unexpected issues, STOP and re-plan immediately — never force progress.

### 2. Subagent Strategy
Use for deep research, parallel independent queries, or context-bloating tasks (10+ file reads, broad exploration); skip for single-file reads or answers you already have. One task per subagent — never delegate synthesis. Subagents must return exact paths / snippets / facts, never generalized summaries.

### 3. Self-Improvement Loop
Write to `tasks/lessons/` (one file per lesson, `status: active`) after: user correction/pushback; self-correction post a failed attempt; a non-obvious technical decision; a surprising bug root cause; a repeated issue; user confirms a non-obvious approach worked.

### 4. Verification Standards
**Definition of Done — all must pass before marking complete:**
- BDD scenario(s) pass via Reqnroll + xUnit; coverage ≥ 80% per Handler class (metric per docs/adr/adr-014-handler-coverage-gate.md: full suite incl. BDD); architecture tests pass (no BC cross-references); each Guard has positive AND negative scenarios.
- Error handling / code quality (critical, zero tolerance): Result-only in the service layer (never `throw` for business logic, never `new Failure()`, never bare-string codes), `CancellationToken cancel` propagated to every I/O call, no `ILogger` in Service/Domain/Handler, no direct BC-to-BC references, FluentAssertions in tests. Full rule text: `.claude/references/dotnet/*.rule.md`, `docs/adr/adr-003-error-handling-and-cross-bc-contracts.md`, `docs/adr/adr-004-failure-shape-and-claude-md-alignment.md`; enforcement registry (which mechanism, when): `docs/verification-matrix.md`.
- Performance (hotpath changes only): P99 < 50ms, throughput ≥ 100 RPS.
- Evidence: always run tests and show output; compare against `main` for behavior-changing work.

### 5. Demand Elegance
Silently ask "is there a more elegant way?" before presenting a solution — surface to the user only on genuine ambiguity/trade-off. A "hack" means find the root cause instead; elegance = simplest correct solution, not the cleverest one.

### Orchestration & Verification
多模型協調與驗證機制的權威來源不在本檔，只放指針（見 `docs/adr/adr-010-norm-doc-discovery-wiring.md`）：
- `docs/orchestration.md` — 多模型協調憲章（角色路由、executor contract、全域停止條件）
- `docs/verification-matrix.md` — 驗證登記表（哪條規則由什麼機制、在什麼時機、由誰驗證）
- `tasks/checkpoint.md` — session 交接的唯一續接入口
- `AGENTS.md` — 非 Claude Code harness 的薄入口

## BDD Scenario Development Cycle

> Development phase only — `.feature` scenarios and API specs are already produced. 凍結的是 Discovery 新場景產出；既有場景修訂、缺陷再現、行為移除走 `docs/adr/adr-022-bdd-requirement-type-routing.md` 分流（§1 需求類型分流表）。

**Kanban**: `tasks/bdd-backlog.md` → `tasks/bdd-progress.md` → ✅ Done. Only the user promotes backlog → progress; Claude MUST NOT do this autonomously. `tasks/bdd-progress.md` is the queue SSOT; find the next scenario via `grep -rn "@ignore" backend/tests/FunctionalTests/Features/ | sort | head -1`.

**Execute via** the `/bdd-vertical-slice` skill (procedure, BC identification, patterns).

**Constraints (always enforced, regardless of skill; mechanized items note their gate)**: never remove more than one `@ignore` at a time unless scenarios share identical new step definitions（pre-commit staged 檢查，豁免用 `ALLOW_MULTI_IGNORE=1`）; never mark done without test output showing it passing; `tasks/bdd-progress.md` update MUST land in the same commit as the implementation（pre-commit staged 檢查 + `scripts/bdd-lint.sh` 帳面一致性）; never commit red (exceptions: confirming a scenario/its steps are unimplemented, immediately before writing them); every `@ignore`-removal commit MUST carry a `Refactor-assessment:` trailer recording the step-9 refactor verdict for both production and test sides（commit-msg hook 機械化強制）; any non-`@ignore` `.feature` change (scenario amendment, defect-repro addition, scenario removal) MUST carry a `Spec-change:` trailer recording the decision basis（commit-msg hook 機械化強制，逃生口 `ALLOW_FEATURE_MAINTENANCE=1` 限機械性整理；見 `docs/adr/adr-022-bdd-requirement-type-routing.md`）.

**Refactor discipline**: production-only (`backend/src/`) or test-only (`backend/tests/`) passes — never mixed — except interface/DTO renames spanning both as the sole change in that commit.

## Task Management Protocol
1. Plan first in `tasks/todo.md` (approval rules: see Working Agreement).
2. Track progress in real time; explain changes after each major step.
3. On completion: add a summary/review to `tasks/todo.md`, write lessons to `tasks/lessons/`.

## Core Principles
- **Minimal Blast Radius**: touch only what the task needs — no side effects, unrelated changes, or speculative abstractions.
- **Root Cause Only**: never band-aid; find and fix the actual cause.
- **Security First**: never introduce injection / hardcoded secrets / OWASP Top 10 issues; validate at boundaries.

## Non-Negotiable Constraints (repeated for attention)

These apply at all times regardless of other instructions:

- CRITICAL: NEVER `throw` for business logic — use `Result<T, Failure>` throughout the service layer. (架構測試強制：`HandlerResultReturnTests.cs`)
- CRITICAL: NEVER commit or complete a task with a failing test suite — Green before every commit and throughout refactoring; TDD's intentional Red (confirming a scenario/its steps are unimplemented) is the only exception. (`scripts/ci-checks.sh` 強制)
- CRITICAL: NEVER remove more than one `@ignore` tag at a time. (pre-commit staged 檢查強制：`scripts/git-hooks/pre-commit`；「identical new step definitions」例外以 `ALLOW_MULTI_IGNORE=1` 明文豁免)
- CRITICAL: NEVER add direct BC-to-BC references — only via SharedKernel interfaces. (架構測試強制：`BoundedContextIsolationTests.cs`)
