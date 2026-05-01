# `.claude/` Hardening — Outstanding Work

Tracking the 7-commit plan from the multi-session hardening pass. Last commit: 2026-04-30 (`8922c47`).

## Done

- [x] **Commit 1** — value-level secret scrubber (`session-init.sh` / observation pipeline)
- [x] **Commit 2** — `session-init.sh` Python source-injection fix
- [x] **Commit 3** — PostToolUseFailure hook
- [x] **Commit 4** — `gh api` permission narrowing to `repos/*`
  - Known limitation: glob does not block mutation flags; documented in commit message.
- [x] **Commit 5** — `609e0bf` per-BC FailureCodes constants + reference docs aligned
  - Added `ConsumerValidationFailureCodes` (SharedKernel/Contracts) and `CreateApiKeyFailureCodes` (KeyLifecycle slice).
  - Updated 5 reference docs (exceptions/async/testing/security/linq + ef-core) to teach the constants pattern instead of the fictional `ErrorCode` enum.
  - Functional-test wire-contract map kept as literals on purpose.

- [x] **Commit 6** — DbContext direct injection reference hardening
  - `ef-core.rule.md` Implicit/Explicit Transaction examples: rewritten as Scoped Repository class with `(AppDbContext db)` primary constructor; added explicit "Background Service / Singleton uses `IDbContextFactory<T>`" exception note next to the §D transaction examples.
  - `exceptions.rule.md` Repository-layer example: rewritten as `OrderRepository(AppDbContext db, ILogger<OrderRepository> logger)` class; the `try/catch (DbException)` body now references `db.Orders` directly.
  - `linq.rule.md` Premature Materialization & In-Memory GroupBy: removed the embedded `await using var context = await _contextFactory.CreateDbContextAsync(cancel);` lines; relies on the same `context` variable already used elsewhere in the file.
  - Verified: no remaining `_contextFactory` / raw `CreateDbContextAsync` in example code; only the two intentional rule statements (§A.40 and the new §D.225 exception note) reference the factory by name.

- [x] **Commit 7** — GitLab/MR workflow reference cleanup
  - `vcs-platform-commands.ref.md`: collapsed two-platform tables to GitHub-only; removed "default to GitLab" backward-compat note; dropped broken pointer to non-existent `code-review-posting-gitlab.ref.md`; rewrote intro line to flag that GitLab support has been removed.
  - `bash.ref.md` / `powershell.ref.md`: deleted GitLab `glab api` POST sections; replaced with cross-repo `gh pr comment -R` form and (bash) GitHub-side marker verification via `gh pr view --json comments`. PowerShell UTF-8 hint reframed against GitHub UI.
  - `code-review/SKILL.md`: rewrote skill description, platform detection, mode logic, decision tree (Step 2/3A), Phase 2.5 Step 2.5.0, output step, skip-review list, Phase 2 PR header, report-template table, and posting-comments reference — all "PR/MR" → "PR", all `glab mr ...` removed; Step 3A no longer branches on platform.
  - `code-review-context-analyzer/SKILL.md`: description and trigger metadata changed from "PR/MR" → "PR"; Step 1 wording updated.
  - Verified: no `gitlab` / `glab` / `merge_requests` / `MR` / `PR/MR` left in any of the five files (the only remaining "GitLab" hit is the new intro line declaring the removal).

## Pending

### ADR-004 acceptance (staged, 2026-05-01)

- [x] ADR-004 Status `Proposed` → `Accepted (2026-05-01)`.
- [x] ADR-004 §4: rewrote NetArchTest snippet as illustrative xUnit reflection test; noted NetArchTest fluent API has no `NotHavePropertyOtherThan(...)`.
- [x] ADR-004 Implementation Rules: added rule that `FailureProvider.CreateFailure` rejects null / whitespace code.
- [x] CLAUDE.md:93 replaced with ADR-004 §3 wording (Handler added to no-ILogger list; removed "embed into Failure message or metadata"; added pointer to ADR-004).
- [x] `FailureProvider.CreateFailure` adds `ArgumentException.ThrowIfNullOrWhiteSpace(code)`.
- [x] New `backend/tests/SharedKernel.Tests/` project with `FailureProviderTests`: valid code returns Failure (1) + null/empty/whitespace/tab/newline throws (5) → 6/6 Green.
- [x] `SharedKernel.Tests` pinned to `FluentAssertions 7.2.0` to comply with `testing.guide.md` (`< 8.0.0`); the FunctionalTests violation remains tracked separately as multi-agent review item #19.
- [x] `ApiKeyManagement.slnx` includes the new test project.
- [x] todo.md cross-doc consistency sweep: (a) removed obsolete "CLAUDE.md still says diagnostic context should be embedded" follow-up; (b) marked multi-agent review item #10 resolved with pointer to ADR-004; (c) item #5 + action-sequence #3 no longer mis-route the hash-algorithm decision through ADR-004.
- [x] `dotnet build` + `dotnet test` Green; `dotnet format --verify-no-changes` clean.

Out of scope here (deferred):

- Real `Architecture.Tests` Failure-shape rule — lands when Architecture.Tests is properly seeded (todo item #20).
- Boundary structured logging for business failures — explicit `不急` per ADR-004 review.
- ADR-005 / ADR-006 acceptance — taken next, only after ADR-004 lands cleanly.

## Non-blocking follow-ups (logged, not scheduled)

- `CreateApiKeyFailureCodes.ValidationErrorPrefix = "VALIDATION_ERROR"` — could tighten to `"VALIDATION_ERROR:"` for stricter prefix matching. Not a regression.
- `Architecture.Tests` discovers 0 tests — pre-existing, unrelated to this hardening pass; investigate later.
- `IServiceScopeFactory` / `FailureProvider` / `IDbContextFactory` cleanup deferred from earlier round — re-evaluate after commit 6. (`FailureProvider` portion handled by ADR-004 acceptance: null/whitespace guard now in place.)
- Value-level secret redaction coverage could be extended for additional token shapes:
  - Slack tokens: `xoxb-` / `xoxp-`
  - GitLab personal access tokens: `glpat-`
  - AWS secret access keys require a separate design decision because they lack a stable prefix and may cause false positives.
- `post-tool-failure.sh` writes `tool_use_id`, `duration_ms`, and `is_interrupt` to `failures.jsonl`, but pending lesson records do not include those trace fields. Consider adding them if pending-to-failure traceability becomes useful.
- `session-init.sh` malformed hook JSON fallback: when payload parsing fails, `TRANSCRIPT_PATH` is empty and lessons may still be injected. This is existing behavior, not a regression; revisit only if stricter hook failure semantics are desired.

## Working principles confirmed in this pass

- Production design is fixed first, **then** references — never the reverse.
- Per-BC failure code constants live with the BC; cross-BC contract codes live next to the contract; no preemptive Common class.
- API contract / wire-format tests intentionally use string literals to lock external behavior (production code uses constants).
- `record Failure(string Code)` — single field; no message/metadata overload exists.

---

## Multi-agent review (2026-04-30) — backlog by classification

A 4-agent parallel review (security / architecture / tests / `.claude` consistency) was run against `backend/`, `.claude/`, `docs/`. Findings cross-referenced against `docs/design/prd.md`, `docs/design/api-spec.md`, `docs/design/design-doc.md`, and current BDD scenarios.

Each item below is tagged 🐞 (real drift to fix), 🏗️ (scaffolding — spec & BDD already plan this work), or ☁️ (out-of-scope — Gateway / infra concern, not this service).

### A. Wire-format / contract drift vs `api-spec.md` 🐞

1. **Endpoint error responses don't follow RFC 9457 ProblemDetails** (`api-spec.md` §2.2). Production `CreateApiKeyEndpoint.cs:42-51` returns `{ error: "..." }`; spec mandates `{ type, title, status, detail, errorCode, errors[] }`. The fallback `Results.Problem(result.Error.Code)` also passes `Code` as `detail` instead of `errorCode`.
2. **`CreateApiKeyResponse.cs` missing `truncatedKey` field** that `api-spec.md` §3.2.1 specifies (e.g. `"...a9B3"` for UI display).
3. **`lifecycleStatus` wire-format inconsistency.** `api-spec.md` example uses PascalCase (`"Active"`); production serialises `apiKey.Status.ToString()` from `ApiKeyStatus` enum which is `ALL_CAPS` (`"ACTIVE"`). Spec query-param contract (`status=Active|Rotating|...`) confirms PascalCase. Fix via EF / JSON value converter; do not rename the enum to PascalCase if `ALL_CAPS` was a deliberate code-side choice — but pick one and align.
4. **`VALIDATION_ERROR` prefix matches too loosely** (`CreateApiKeyEndpoint.cs:49`, `CreateApiKeyFailureCodes.cs:11`). Tighten to `"VALIDATION_ERROR:"` so unrelated codes starting with the same letters can't accidentally fall into 400. Already in old follow-ups; promoted to actionable.

### B. Security: PRD-mandated invariants not yet honoured 🐞

5. **Hash algorithm violates `prd.md` §5.2 + R-SEC-03.** Spec mandates Argon2id or PBKDF2-SHA256; `ApiKey.cs:101` uses `BCrypt.HashPassword(rawKey, workFactor: 4)`. Pick one of: (a) replace with Argon2id (`Konscious.Security.Cryptography.Argon2`), (b) replace with HMAC-SHA256(server-pepper, rawKey) — keys are 128-bit random so HMAC is technically sufficient and far faster on the hot path, (c) keep BCrypt and document the deviation in an ADR. **Independent of choice**, raise work factor / cost params to 2026 minimum. Decision needs a dedicated hash-algorithm ADR (ADR-004 has been taken by the Failure-shape decision; this would be a new ADR — likely ADR-007 or whatever number is next free at the time).
6. **`BCrypt.Net-Next` version mismatch** between `KeyLifecycle/.csproj:5` (`4.0.3`) and `Infrastructure/.csproj:13` (`4.1.0`). Resolve before #5 is decided so we ship one version.
7. **Active-key-count guard is TOCTOU under concurrency** (`CreateApiKeyHandler.cs:23-32`). BDD `01_CreateApiKey.feature:38` covers the happy-path "limit reached" case but not the race. Add unique partial index or `IsolationLevel.Serializable` + treat `DbUpdateException` (unique violation) as the limit/duplicate failure.
8. **Constant-time comparison reminder for the future validation hot-path** (`prd.md` R-VAL-01). Not yet implemented; flag in the validation slice when it lands. Use `CryptographicOperations.FixedTimeEquals` over byte arrays — never `string ==`.
9. **Dev `appsettings.Development.json:9` commits a plaintext DB password.** Move to User Secrets / env var even for dev to avoid normalising the pattern.

### C. Architecture / code drift 🐞

10. ~~**`Failure(string Code)` vs `CLAUDE.md:93` wording contradiction.**~~ ✅ Resolved by ADR-004 acceptance (2026-05-01): CLAUDE.md:93 rewritten to match the single-field `Failure` shape; rationale and Implementation Rules captured in `docs/adr/adr-004-failure-shape-and-claude-md-alignment.md`.
11. **`testing.guide.md` mock examples still use `_orderRepository.` / `_orderService.` field-style** (Sections C / E / J — 14 line hits). Hardening pass aligned `exceptions.rule.md` / `async.rule.md` / `security.rule.md` to primary-constructor convention but missed this file. NSubstitute test fixtures may still legitimately use private fields for the mock, but the call sites inside test bodies should match production parameter names.
12. **`code-review/SKILL.md` Phase 3 reference table** lists `nodejs/` and `python/` as if they always exist; only `general/` and `dotnet/` exist. Skill execution would fail-load. Add a "skip if missing directory" precondition to lines 96-97 / 476-481.
13. **`settings.local.json:8` uses non-standard pattern `dotnet format *`** (with space) — rest of the file uses `Bash(dotnet ...:*)`. Change to `Bash(dotnet format:*)` for consistency and to avoid quiet match-fail.
14. **`ITenantQueryContext` registration location.** Currently `InfrastructureModule.cs:25`. Per BC-isolation principle the abstraction belongs to TenantManagement; register in `TenantManagementModule` (Infrastructure can expose a small `AddAppDbContext` helper for the BC module to call). Or document the placement.
15. **`ScopeRegistryEntry` record declared inside `AppDbContext.cs:28`.** Should live in `KeyLifecycle/Domain/` (where `IScopeRegistry` lives) so Infrastructure isn't owning a domain record.
16. **`ScopeRegistryService` named as `*Service` but is a Repository** (`Infrastructure/Persistence/Repositories/ScopeRegistryService.cs`). Rename to `ScopeRegistryRepository` for naming consistency with `ApiKeyRepository`, `AccessPolicyRepository`.
17. **Anaemic `ApiKey` aggregate.** All invariants (limit, name uniqueness, scope existence, expiry) live in the Handler; `ApiKey.Create` only constructs the entity. At least the intrinsic invariants (`expiresAt > now`, `scopes.Any()`) should move into the aggregate constructor. Cross-aggregate guards (count, name uniqueness) stay in the Handler — they need DB access.
18. **`Result<TValue, TError>` double implicit conversion is a latent footgun.** No call site uses `Result<X, X>` today, but if anyone ever returns `Result<string, string>` the conversion is ambiguous. Consider explicit `Result.Ok(value)` / `Result.Fail(err)` factories or constraining `TError` to `Failure` everywhere.

### D. Test infrastructure 🐞

19. **`FluentAssertions 8.9.0` license non-compliance.** `tests/FunctionalTests/*.csproj:12`. Pin to `7.2.0` (last permissive-license OSS) or migrate to `AwesomeAssertions` / `Shouldly`. `testing.guide.md` already mandates `< 8.0`. Action before next release.
20. **`Architecture.Tests` is a stub** — zero `.cs` files, no `NetArchTest.Rules` PackageReference. The "0 tests discovered" already in todo is more than a discovery issue; nothing is *written*. Seed minimum tests:
    - BC-isolation (`Types.InAssembly(...).Should().NotHaveDependencyOn(...)` between BCs except via `SharedKernel.Contracts`)
    - Repository must not return `Result<T, Failure>`
    - No `ILogger<>` in Domain / Service / Handler
    - Naming: `*Handler` / `*Repository` / `*FailureCodes`
21. **`ApiKeyManagementWebApplicationFactory.cs:28-29` uses `Environment.SetEnvironmentVariable(...)`** — process-wide leakage; parallel test classes will see last-write-wins. Switch to `builder.UseSetting(...)` or `ConfigureAppConfiguration`.
22. **`TestHooks.cs:103-105` opens a fresh `NpgsqlConnection` per `ResetAsync`** instead of reusing the long-lived checkpoint connection at line 69. Pure overhead.
23. **Testcontainers don't use `.WithReuse(true)`** — each local dev iteration pays the full container startup. Add reuse for dev productivity (CI behaviour unchanged).
24. **No test data builders in `TestInfrastructure/`.** As more BCs come online each step file will repeat seed code. Add a `TenantBuilder` / `ApiKeyBuilder` etc. before the next BDD wave.

### E. Scaffolding — already in spec / BDD plan, no separate action 🏗️

25. **No auth on `CreateApiKeyEndpoint`** — `api-spec.md` §2.1 fully defines JWT roles (`PlatformAdmin`, `TenantAdmin`, `Consumer`, `SecurityAdmin`) and IDOR cross-validation rules. Implementation will land with the auth middleware slice; flagged here so reviewers don't re-raise.
26. **No FluentValidation / `Request` charset & length checks.** `api-spec.md` §3.2.1 lists all validation rules; partial Handler guards exist (`scopes_empty`, `expires_at_past`, `expires_at_exceeds_max`). Will be replaced by FluentValidation when input-validation slice lands.
27. **`Audit` and `Monitoring` BC projects are empty shells.** `design-doc.md` §3.2 defines both as Supporting Domain. BDD scenarios in `02_RevokeKey.feature` ... `06_ExpireKey.feature` are mostly `@ignore`d. They land per BDD wave.
28. **`Features/02–06/*.feature` step definitions don't exist yet.** Will be added one scenario at a time per the `@ignore`-driven BDD cycle.

### F. Out of this service's scope ☁️

29. **HTTPS redirect / HSTS** — `prd.md` R-SEC-04 mandates HTTPS but `design-doc.md` §6.1 places TLS termination at the API Gateway. This service runs behind it.
30. **Per-tenant rate limiting and burst control** — `design-doc.md` §6.3 and §7.2.2 explicitly assign rate limiting / quota enforcement to the API Gateway. `RateLimitConfig` is part of `AccessPolicy` but enforcement happens at the Gateway. Do **not** add `AddRateLimiter` to this service.
31. **CORS** — Gateway boundary concern; not configured here intentionally.

### G. Already logged elsewhere (kept here for cross-reference) ℹ️

32. Slack `xoxb-`/`xoxp-`, GitLab `glpat-` token shapes not yet covered by hook redaction. (Existing item above.)
33. `post-tool-failure.sh` trace fields (`tool_use_id`, `duration_ms`, `is_interrupt`) not mirrored in pending lesson records. (Existing item above.)
34. `session-init.sh` malformed-payload fallback semantics. (Existing item above.)

---

## Recommended action sequence

1. **Now** — items #6, #9, #11, #12, #13 (low-risk doc/config tightening; no production-code design impact).
2. **Before next BC wave (Audit/Monitoring)** — items #1, #2, #3, #4 (wire-format alignment) so subsequent BCs inherit the right contract shape; #20 (Architecture.Tests seed) so new BC code is enforced from day one.
3. **Before validation hot-path implementation** — items #5 (hash algo decision + dedicated hash-algorithm ADR — *not* ADR-004, which is now Failure shape), #7 (concurrency guard + unique index), #8 (constant-time compare reminder added to `exceptions.rule.md` or `security.rule.md`).
4. **Before any release** — item #19 (FluentAssertions license).
5. **Opportunistic / housekeeping** — items #10, #14–#18, #21–#24.
