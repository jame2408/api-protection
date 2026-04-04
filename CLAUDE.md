# Claude Code Rules & Workflow

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

## Workflow Orchestration

### 1. Plan-First Approach

**Enter Plan Mode when ANY of the following apply:**
- Task touches 3+ files with interdependent changes
- Task involves architectural decisions (new BC slice, new dependency, schema change, integration contract)
- The wrong approach would cause significant rework
- A previous attempt at this task failed

**Skip Plan Mode when:**
- Change is confined to a single file and the solution is clear
- It's a hotfix, typo, or rename
- The task is reversible with no downstream impact

- **Pivot:** If a task deviates or encounters unexpected issues, STOP and re-plan immediately. NEVER force progress.
- **Specificity:** Write detailed technical specifications upfront to eliminate ambiguity.

### 2. Subagent Strategy

- Use subagents for: deep research, parallel independent queries, tasks that would bloat the main context (e.g. reading 10+ files, broad codebase exploration).
- DO NOT use subagents for: single-file reads, simple searches, tasks answerable in 1-2 tool calls, or anything where you already have the answer.
- One specific task per subagent; NEVER delegate synthesis or decision-making to a subagent.
- Subagents MUST return exact file paths, accurate code snippets, or explicit factual answers. NEVER accept generalized summaries or speculative logic from a subagent.

### 3. Self-Improvement Loop

Write to `tasks/lessons.md` after ANY of the following:
  - User correction or pushback ("no", "don't", "change it to...")
  - I self-correct after a failed command, wrong approach, or misunderstanding
  - A non-obvious technical decision is made (architecture, library choice, tradeoff)
  - A bug's root cause is non-trivial or surprising
  - A repeated issue appears for the second time
  - User confirms a non-obvious approach worked ("yes exactly", "perfect", accepting an unusual choice)

### 4. Verification Standards

**Definition of Done — ALL must pass before marking complete:**

_Tests:_
- BDD scenario(s) covering the change pass via Reqnroll + xUnit
- Unit test coverage ≥ 80% for Handler code
- Architecture tests pass (no BC cross-references via NetArchTest)
- Each Guard condition has positive AND negative scenario

_Error Handling (Critical — zero tolerance):_
- CRITICAL: Service layer uses `Result<T, Failure>` — NEVER `throw` for business logic
- NEVER use `new Failure()` — all failures created via `FailureProvider.CreateFailure()`
- NEVER access `.Value` without checking `.IsFailure` first
- NEVER use empty catch blocks; NEVER use `throw ex;` (use `throw;`)
- NEVER inject `ILogger` into Service or Domain layers. Embed diagnostic context (entity IDs, input values) into the `Failure` message or metadata so boundary loggers (Middleware, Pipeline Behavior) can produce meaningful logs without service-layer coupling.

_Code Quality:_
- Async methods must accept `CancellationToken cancel` and propagate it to every I/O call — EF Core queries, `SaveChangesAsync`, HTTP clients, and message bus operations. NEVER silently drop it.
- Naming conventions: PascalCase methods, `_camelCase` fields, `Async` suffix on async methods
- NEVER add direct BC-to-BC references (only via SharedKernel interfaces)
- FluentAssertions used in tests, not direct comparison

_Performance (for hotpath changes):_
- API Key validation latency P99 < 50ms
- Validation throughput ≥ 100 RPS

_Evidence:_
- Always run tests and provide output to demonstrate correctness
- For changes touching existing behavior, compare against master branch before marking done

### 5. Demand Elegance

- Before presenting a solution, silently evaluate: "Is there a more elegant way?" — assess internally first; only ask the user if evaluation reveals a genuine ambiguity or tradeoff that requires their input.
- If a fix feels like a "hack," find the root cause and implement the proper solution instead.
- Avoid over-engineering: elegance means the simplest correct solution, not the cleverest one.
- **Exception — always ask, never assume:** business requirements, domain logic, business flows, and use case intent. If the correct behavior depends on domain knowledge the user holds, stop and ask rather than guessing.

### 6. Autonomous Bug Fixing

- Proactive: When given a bug report, resolve it autonomously without requesting step-by-step guidance.
- Evidence-Led: Analyze logs, error traces, and failing tests to isolate the root cause.
- Efficiency: Minimize user context-switching; fix failing CI tests independently unless blocked.
- Completion: After fixing, run through the §4 Verification Standards checklist before marking done.

## BDD Scenario Development Cycle

All 44 BDD scenarios are tracked in `docs/bdd/implementation-order.md`.
Unimplemented scenarios are tagged `@ignore` in their `.feature` files so the test suite stays Green at all times.

**Each session, follow this cycle:**

1. Open `docs/bdd/implementation-order.md` and find the next unimplemented scenario.
2. Remove the `@ignore` tag from that scenario in its `.feature` file.
3. Run tests → confirm **Red** (missing step or failing assertion).
4. Implement the production code and step definitions needed.
5. Run tests → confirm **Green** (target scenario passes, all others still pass/skip).
6. **Refactor** — improve code quality without changing behaviour (see rules below).
7. Run tests → confirm still **Green** after refactoring.
8. Update `docs/bdd/implementation-order.md`: mark the scenario ✅ and increment the "已通過" count.
9. Commit, then loop back to step 1 for the next scenario.

**Rules:**
- Never remove more than one `@ignore` at a time unless scenarios share the exact same new step definitions.
- Never mark a scenario done unless the test output shows it passing.
- NEVER proceed if the test suite has failures (must be Green after every code change).

**Refactor Rules (Step 6):**
- **Production refactor**: only touch `backend/src/` — NEVER change `backend/tests/`
- **Test refactor**: only touch `backend/tests/` — NEVER change `backend/src/`
- NEVER mix both in the same refactor pass; run tests after each pass to confirm Green.
- Exception: interface or DTO renames that span both may be done in a single pass, but MUST be the only change in that commit.
- Checklist for production refactor:
  - Verify compliance with `.claude/references/dotnet/*.rule.md` (Result pattern, `cancel` naming, DI lifetime)
  - Eliminate duplication introduced by this scenario's implementation
  - Ensure naming conventions (PascalCase methods, `_camelCase` fields, `Async` suffix)
- Checklist for test refactor:
  - Step definitions reusable across scenarios (no copy-paste Given/When/Then bodies)
  - NEVER access DB directly in When steps — actions must trigger via domain/API boundaries
  - In Then steps, prefer API/observable state assertions. Only access DB directly if the expected state change is internal and not exposed via any public contract.
  - Each step has a single, clear responsibility

## Task Management Protocol

1. Plan First: Document the execution plan in tasks/todo.md with checkable items, then get user approval before starting. Exception: bug fixes proceed autonomously per §6 without requiring prior approval.
2. Track Progress: Mark items as complete in real-time.
3. Explain Changes: Provide a high-level summary after completing each major step.
4. Document Results: Add a summary/review section to tasks/todo.md upon completion, then write any lessons to tasks/lessons.md.

## Core Principles

- Minimal Blast Radius: Touch only the code necessary for the task. DO NOT introduce side effects, unrelated changes, or speculative abstractions.
- Root Cause Only: NEVER apply band-aid fixes. Always find and fix the actual cause.
- Security First: Never introduce command injection, hardcoded secrets, or OWASP Top 10 vulnerabilities. Validate at system boundaries only.

## Non-Negotiable Constraints (repeated for attention)

These apply at all times regardless of other instructions:

- CRITICAL: NEVER `throw` for business logic — use `Result<T, Failure>` throughout the service layer.
- CRITICAL: NEVER proceed with a failing test suite — suite must be Green after every change.
- CRITICAL: NEVER remove more than one `@ignore` tag at a time.
- CRITICAL: NEVER add direct BC-to-BC references — only via SharedKernel interfaces.