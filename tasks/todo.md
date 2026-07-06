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
- Lessons triage 常設觸發：`tasks/lessons/` 內 `status: active` 檔案數 ≥ 15、或 phase 收尾時，盤點 active 條目可否機械化（腳本/lint/gate），可機械化者落地後改該檔 frontmatter 為 `status: archived`（判準：`docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (b)；載體見 `docs/adr/adr-021-shared-state-files-team-scale.md`）。
- ADR-004 允許 ILogger 邊界清單第 6 類（Infrastructure ApiClient，`LoggerBoundaryTests` 與 `exceptions.rule.md` §B 已實質採用）待正名——掛到下一份錯誤處理相關 ADR，不單開。
- `requirements-analysis-design` skill 觸發詞與 `.feature` 凍結相撞——根治在 upstream `jame2408/agent-skills` repo 加凍結 gate，本地已在 `tasks/bdd-backlog.md` 檔頭布防。

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

4. **`VALIDATION_ERROR` prefix matches too loosely** (`CreateApiKeyEndpoint.cs:49`, `CreateApiKeyFailureCodes.cs:11`). Tighten to `"VALIDATION_ERROR:"` so unrelated codes starting with the same letters can't accidentally fall into 400. Already in old follow-ups; promoted to actionable.

### B. Security: PRD-mandated invariants not yet honoured 🐞

5. **已裁決 2026-07-05（ADR-017：HMAC-SHA256 + pepper，實作同 commit 落地）** — **Hash algorithm violates `prd.md` §5.2 + R-SEC-03.** Spec mandates Argon2id or PBKDF2-SHA256; `ApiKey.cs:101` uses `BCrypt.HashPassword(rawKey, workFactor: 4)`. Pick one of: (a) replace with Argon2id (`Konscious.Security.Cryptography.Argon2`), (b) replace with HMAC-SHA256(server-pepper, rawKey) — keys are 128-bit random so HMAC is technically sufficient and far faster on the hot path, (c) keep BCrypt and document the deviation in an ADR. **Independent of choice**, raise work factor / cost params to 2026 minimum. Decision needs a dedicated hash-algorithm ADR (ADR-004 has been taken by the Failure-shape decision; this would be a new ADR — likely ADR-007 or whatever number is next free at the time).
7. **Active-key-count guard is TOCTOU under concurrency** (`CreateApiKeyHandler.cs:23-32`). BDD `01_CreateApiKey.feature:38` covers the happy-path "limit reached" case but not the race. Add unique partial index or `IsolationLevel.Serializable` + treat `DbUpdateException` (unique violation) as the limit/duplicate failure.
8. **Constant-time comparison reminder for the future validation hot-path** (`prd.md` R-VAL-01). Not yet implemented; flag in the validation slice when it lands. Use `CryptographicOperations.FixedTimeEquals` over byte arrays — never `string ==`（ADR-017 Implementation Rule 6 已固化此義務）。

### C. Architecture / code drift 🐞

11. **`testing.guide.md` mock examples still use `_orderRepository.` / `_orderService.` field-style** (Sections C / E / J — 14 line hits). Hardening pass aligned `exceptions.rule.md` / `async.rule.md` / `security.rule.md` to primary-constructor convention but missed this file. NSubstitute test fixtures may still legitimately use private fields for the mock, but the call sites inside test bodies should match production parameter names.
12. **`code-review/SKILL.md` Phase 3 reference table** lists `nodejs/` and `python/` as if they always exist; only `general/` and `dotnet/` exist. Skill execution would fail-load. Add a "skip if missing directory" precondition to lines 96-97 / 476-481.
13. **`settings.local.json:8` uses non-standard pattern `dotnet format *`** (with space) — rest of the file uses `Bash(dotnet ...:*)`. Change to `Bash(dotnet format:*)` for consistency and to avoid quiet match-fail.
14. **`ITenantQueryContext` registration location.** Currently `InfrastructureModule.cs:25`. Per BC-isolation principle the abstraction belongs to TenantManagement; register in `TenantManagementModule` (Infrastructure can expose a small `AddAppDbContext` helper for the BC module to call). Or document the placement.
15. **`ScopeRegistryEntry` record declared inside `AppDbContext.cs:28`.** Should live in `KeyLifecycle/Domain/` (where `IScopeRegistry` lives) so Infrastructure isn't owning a domain record.
16. **`ScopeRegistryService` named as `*Service` but is a Repository** (`Infrastructure/Persistence/Repositories/ScopeRegistryService.cs`). Rename to `ScopeRegistryRepository` for naming consistency with `ApiKeyRepository`, `AccessPolicyRepository`.
17. **Anaemic `ApiKey` aggregate.** All invariants (limit, name uniqueness, scope existence, expiry) live in the Handler; `ApiKey.Create` only constructs the entity. At least the intrinsic invariants (`expiresAt > now`, `scopes.Any()`) should move into the aggregate constructor. Cross-aggregate guards (count, name uniqueness) stay in the Handler — they need DB access.
18. **`Result<TValue, TError>` double implicit conversion is a latent footgun.** No call site uses `Result<X, X>` today, but if anyone ever returns `Result<string, string>` the conversion is ambiguous. Consider explicit `Result.Ok(value)` / `Result.Fail(err)` factories or constraining `TError` to `Failure` everywhere.

### D. Test infrastructure 🐞

20. **`Architecture.Tests` is a stub** — zero `.cs` files, no `NetArchTest.Rules` PackageReference. The "0 tests discovered" already in todo is more than a discovery issue; nothing is *written*. Seed minimum tests:
    - **Status (2026-06-13, MVP 三條已落地 — 見 `tasks/process-improvement-plan.md` §8.2)**：NetArchTest.Rules 1.3.2 + FluentAssertions 7.2.0 加入；7 tests 綠（綠＋故意紅驗證）。
    - [x] BC-isolation (`BoundedContextIsolationTests.cs`，NetArchTest，5 BC 配對；carve-out = SharedKernel)
    - [x] Repository must not return `Result<T, Failure>` (`RepositoryReturnTypeTests.cs`，reflection)
    - [x] Service/Handler 必回 `Result<T, Failure>` (`HandlerResultReturnTests.cs`，reflection，鎖定 `*Handler`；contract `*Service` 豁免)
    - [x] No `ILogger<>` in Domain / Service / Handler (`LoggerBoundaryTests.cs`，reflection ctor-param)
    - [x] Naming: `*Handler` / `*Repository` / `*FailureCodes` (`NamingConventionTests.cs`，reflection — 介面實作命名 + FailureCodes static/const-string shape)
    - [x] 語法層級(reflection 看不到)→ `scripts/source-lint.sh`(接進 ci-checks.sh)：禁 `new Failure(`(豁免 FailureProvider)、`CancellationToken` 必名 `cancel`
    - **第二批 (2026-06-13) 全數落地;Architecture.Tests 共 11 tests 綠(各綠＋故意紅驗證)。**
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

---

## Cross-doc consistency sweep (2026-05-31)

Deep cross-check of ADRs ↔ CLAUDE.md ↔ `.claude/references` ↔ production code ↔ `docs/design` + `docs/bdd` ↔ `.feature` ↔ skills. Decision-chain semantics verified **clean** (ADR-001/002/003/004/005/006 all hold against code: enum PascalCase 0-hit, `Failure` single-field, no MediatR, no Service/Domain/Handler `ILogger`, ADR-005 §6 sync comments landed, "121 scenarios" = 44+15+13+30+19 confirmed exact).

### Status refresh against the 2026-04-30 backlog

- ✅ Newly confirmed resolved since the review: **#12** (`code-review/SKILL.md` nodejs/python table — 0 hits), **#13** (`settings.local.json` `dotnet format` pattern — 0 hits), **#11** (resolved-by-decision: ADR-005 scoped test fixtures out + added `testing.guide.md:46` caveat).
- ❌ Re-confirmed still open: **#6** (BCrypt 4.0.3 vs 4.1.0), **#9** (dev plaintext DB password), **#19** (FluentAssertions 8.9.0 vs 7.2.0), **#20** (Architecture.Tests stub — 0 `.cs` files).
- This sweep's findings that were **already tracked**: Architecture.Tests empty = #20; FluentAssertions split = #19; BCrypt split = #6. No duplicate entries created for these.

### Genuinely new items (prioritized)

- [ ] **38. (P3, doc erratum — needs mechanism decision)** ADR-002 §3 `tests/` tree omits `SharedKernel.Tests` (present in `.slnx`). Illustrative tree, low impact, but doc trails reality.
  - ⚠️ #37/#38 touch Accepted ADR bodies → governance clause "任何提案修改 1–N 必須先開新 ADR" applies. Decide **erratum note vs new ADR** before editing; do **not** silently edit ADR bodies.
- [x] **39. ✅ 2026-07-05（登記滯後補記）** 已隨 #20 落地自動消解 — Architecture.Tests 現有 14 個測試（含動態發現的 BC 隔離 Theory），CLAUDE.md DoD 的架構測試 gate 不再空轉。原描述：CLAUDE.md "Definition of Done" lists "Architecture tests pass" as an active gate, but Architecture.Tests is an empty shell (#20) so the gate is currently vacuous.

---

## Recommended action sequence

1. **Now** — items #6, #9, #11, #12, #13 (low-risk doc/config tightening; no production-code design impact).
2. **Before next BC wave (Audit/Monitoring)** — items #1, #2, #3, #4 (wire-format alignment) so subsequent BCs inherit the right contract shape; #20 (Architecture.Tests seed) so new BC code is enforced from day one.
3. **Before validation hot-path implementation** — items #5 (hash algo decision + dedicated hash-algorithm ADR — *not* ADR-004, which is now Failure shape), #7 (concurrency guard + unique index), #8 (constant-time compare reminder added to `exceptions.rule.md` or `security.rule.md`).
4. **Before any release** — item #19 (FluentAssertions license).
5. **Opportunistic / housekeeping** — items #10, #14–#18, #21–#24.

---

## Coverage gate（QA 強化 #1；方向已核准 2026-07-05，計畫待核准）

**目標**：機械化 DoD「unit coverage ≥ 80% for Handler code」（`docs/verification-matrix.md` 無防線區塊第 1 條）。

**基線實測（2026-07-05，4/44 場景時點）**：`CreateApiKeyHandler` 89.1%（含 async state machine 合併：49/55 行）、`ConsumerValidatorService` 100% → 80% 門檻上線即綠，且隨場景解鎖單調上升，無需 ratchet。

**設計裁決（將載入 ADR-014）**：
1. **度量來源 = 全測試套件**（含 BDD FunctionalTests）：本專案 BDD-first，Handler 覆蓋天生來自場景；「unit-only」解讀會逼出與場景重複的測試，違反工作流。
2. **鎖定對象 = concrete `*Handler` 類別**（compiler-generated async state machine 併回母類計算），與 `HandlerResultReturnTests` 同一鎖定邏輯；**每類各自 ≥ 80%**，非全體聚合。
3. **時機層 = full gate（push 前 / CI）**：Handler 覆蓋依賴 FunctionalTests（需 Docker），fast 層放不進。
4. **機制 = coverlet.collector（既有依賴，零新增套件）+ `scripts/coverage-check.sh`**（python3 解析 cobertura XML，多報表以 per-line max hits 合併）。

**Alternatives（ADR 詳述）**：coverlet.msbuild `/p:Threshold`（assembly 級門檻，無 class 級精度 → Rejected）；ReportGenerator（新增工具依賴，python 解析已足 → Rejected）；unit-test-only 度量（違 BDD-first → Rejected）。

**步驟**：
- [x] orchestrator 起草 `docs/adr/adr-014-handler-coverage-gate.md`（自 `_template.md`，含同步項目清單）。
- [x] executor（依 `tasks/_templates/executor-spec.md` 派工）：`scripts/coverage-check.sh` + `scripts/ci-checks.sh` full 接線 + `docs/verification-matrix.md` 同 commit 更新（主表新增一行、無防線區塊移出該條）+ 新檢驗「綠＋故意紅」驗證（門檻暫調 95 證紅後還原）。

**關聯**：QA #2 變異測試（Stryker.NET）排 Wave 1 全綠後觸發（見 `tasks/checkpoint.md` 下一步）。

---

## 團隊尺度共享檔 + BDD 需求分流（規劃 2026-07-06，待使用者裁決後執行）

> 兩案皆為使用者主動提出的需求（非投機性立法，不違制度凍結啟發式）。既往 ADR grep 反查完畢：無同案裁決（ADR-019 Alternatives 拒絕的是「歸檔判準擴充」；BDD 側從無行為變更流程裁決）。

### 案一：共享狀態檔的多寫者衝突（→ ADR-021）

衝突根因 = 多寫者 × 同檔同行。依寫入模式分三類，處方不同：

| 檔案 | 寫入模式 | 多寫者壓力 | 處方 |
|---|---|---|---|
| `tasks/lessons/` | 累積型（每 session 追加＋triage 搬移） | **高** | 一檔一教訓：`tasks/lessons/YYYYMMDD-slug.md` + frontmatter `status: active\|archived`；歸檔=改單檔 frontmatter；`session-init.sh` 改 glob 注入 active 條目（注入上限維持 ADR-008 原則）— ✅ 2026-07-06 已落地（ADR-021） |
| `tasks/checkpoint.md` | 快照型（每 session 覆寫） | 中（單協調者時=0） | ADR 內定分流規格：`tasks/checkpoints/<workstream>.md`＋觸發條件「出現第二個常設寫者」才實作；憲章 §6 冷啟動 prompt 同步修訂 |
| `tasks/bdd-progress.md` | 帳面型（可自 `@ignore` 推導） | 低（場景實作因 build-gate 並行規則天然串行） | 帳面生成化：計數欄由腳本自 grep 重產（`bdd-lint.sh` 檢查邏輯反轉為產生器）；「同 commit」規則不變 |
| `tasks/todo.md` / `bdd-backlog.md` | 混合 / 低頻 | 低 | 明文不處置（non-goal），事故驅動再議 |

同步項目（executor 需 grep 反查逐字引用者，已知：`session-init.sh`、`lesson` skill、ADR-008/013 引用、CLAUDE.md §3、本檔 triage 常設觸發、`docs/verification-matrix.md`）。Alternatives 需載：`.gitattributes merge=union`（Rejected：Active/Archived 分區邊界追加會交錯、triage 搬移仍衝突）。

### 案二：BDD 流程 × 需求類型分流（→ ADR-022）

現行流程只覆蓋「新功能且場景已預產」。分流表（ADR 本體）：

| 需求類型 | 規格動作 | 紅的形態 | 缺口 |
|---|---|---|---|
| 新功能（場景在 backlog） | 使用者晉升 → 移 `@ignore` | Pending／自然紅（啟用型補故意紅） | 無，現制完備 |
| **既有行為變更** | 使用者裁決 → 修改既有場景（spec-first）→ 修實作 | 修場景後自然紅=舊實作不符新規格；若直接綠=修訂無效，必停回報 | **主缺口**：`@ignore` 機制不觸發；`.feature` 凍結條款誤傷；無 gate |
| 缺陷修復 | 先補再現（場景或 unit test）證紅 → 修 → 綠 | 再現紅 | 再現場景需明文豁免凍結與 backlog 晉升（缺陷驅動=事故驅動）；帳面 +1 同 commit |
| 行為移除 | 使用者裁決 → 刪場景＋產碼同 commit | 無（刪後仍綠） | bdd-lint 需容忍分母遞減；明文裁決權 |
| 純重構 | 禁碰 `.feature` | 無 | 無，Refactor discipline 已覆蓋 |
| 非功能 | 不進 BDD 看板，路由到對應 gate（coverage／perf／security，見矩陣） | gate 故意紅 | 無 |

行為變更機械化（鏡射 `Refactor-assessment:` 模式）：staged `.feature` diff 觸及**非 `@ignore` 場景**時，commit 須帶 `Spec-change:` trailer（記使用者裁決依據）且同 commit 含對應 steps／production 變更；場景修訂與實作同 commit（never-commit-red 不破例，紅只存在於工作區、以測試輸出取證）。CLAUDE.md「Do not author new `.feature` files」措辭拆分：凍結的是 Discovery 新場景產出，不含既有場景修訂與缺陷再現。

### 執行編排（裁決後）

1. 小型模型（haiku）：全 repo 引用掃描（`tasks/lessons/`／`checkpoint.md`／`bdd-progress.md`／`@ignore` 的逐字引用者清單）→ 餵 ADR 同步項目。
2. ADR-021、ADR-022 起草（自 `_template.md`，過 `adr-lint.sh`）。
3. ✅ 2026-07-06 完成 — Executor A（中型）：lessons 拆檔遷移＋`session-init.sh` glob＋引用全修，綠＋故意紅（合成 payload 注入測試）。
4. Executor B（中型）：bdd-lint／pre-commit／commit-msg 擴充＋CLAUDE.md／SKILL 措辭同 commit，綠＋故意紅（三面取證）。
5. A、B **串行**（兩者皆觸 CLAUDE.md，檔案集相交）；orchestrator 親自驗證後放行 commit。

## Archived（已結案）

1. ~~**Endpoint error responses don't follow RFC 9457 ProblemDetails.**~~ ✅ Resolved（2026-07-05 盤查確認，登記滯後補記）— `backend/src/KeyLifecycle/Http/ApiProblem.cs` 為單一 error envelope 來源（`type`/`title`/`status`/`errorCode`/`traceId` + `application/problem+json`），BDD `ThenCreateFailsWithReason` 逐欄鎖定 wire format（`docs/verification-matrix.md` 第 17 項），現行 7 個通過場景全數驗證。
2. ~~**`CreateApiKeyResponse.cs` missing `truncatedKey` field.**~~ ✅ Resolved（2026-07-05 盤查確認，登記滯後補記）— `CreateApiKeyResponse` 已含 `TruncatedKey`，`ThenRawKeyIsReturned` 斷言 `"..." + rawKey[^4..]`（`docs/verification-matrix.md` 第 18 項）。
3. ~~**`lifecycleStatus` wire-format inconsistency.**~~ ✅ Resolved by ADR-006 (2026-05-02). Decision opposite to the original suggestion: `ApiKeyStatus` enum **was** renamed to PascalCase, paired with `JsonStringEnumConverter(allowIntegerValues: false)`; DTO type changed from `string` to `ApiKeyStatus`; functional tests now lock raw JSON wire literal `"Active"` via `JsonDocument`.
6. ~~**`BCrypt.Net-Next` version mismatch** between `KeyLifecycle/.csproj:5` (`4.0.3`) and `Infrastructure/.csproj:13` (`4.1.0`).~~ ✅ Resolved (2026-07-04) via #36 — `backend/Directory.Packages.props` now centrally pins `BCrypt.Net-Next` to `4.1.0` for both projects.
9. ~~**Dev `appsettings.Development.json:9` commits a plaintext DB password.** Move to User Secrets / env var even for dev to avoid normalising the pattern.~~ ✅ Resolved (2026-07-04) — `Password=` 段自 `appsettings.Development.json` 移除，Host 專案加 `<UserSecretsId>`，本機設定指令寫入 root `README.md`。使用者裁決：不重寫 git 歷史（localhost postgres/postgres 為通用預設值，無保密價值）。
10. ~~**`Failure(string Code)` vs `CLAUDE.md:93` wording contradiction.**~~ ✅ Resolved by ADR-004 acceptance (2026-05-01): CLAUDE.md:93 rewritten to match the single-field `Failure` shape; rationale and Implementation Rules captured in `docs/adr/adr-004-failure-shape-and-claude-md-alignment.md`.
19. ~~**`FluentAssertions 8.9.0` license non-compliance.** `tests/FunctionalTests/*.csproj:12`.~~ ✅ Resolved (2026-07-04) via #36 — `backend/Directory.Packages.props` now centrally pins `FluentAssertions` to `7.2.0` for every test project, including `FunctionalTests`.
- [x] **35. ✅ 2026-07-04** `tasks/bdd-progress.md` residual `ROTATING` → `Rotating` 已修正（本 housekeeping commit）。原描述： Last ALL_CAPS lifecycle literal after ADR-006's PascalCase unification. Out of ADR-006 §9 declared scope (`tasks/` not listed) so the acceptance grep missed it; still a real drift from the "code/DB/wire/docs aligned" goal. One-line fix, zero design impact.
- [x] **36. (P2, pair with #6/#19)** ✅ Resolved (2026-07-04). Introduced `backend/Directory.Packages.props` (`ManagePackageVersionsCentrally=true`); every `PackageReference` across all 12 `.csproj` files now omits `Version` and resolves centrally. Root-cause fix for #6 (BCrypt pinned to `4.1.0`) and #19 (FluentAssertions pinned to `7.2.0` repo-wide). Packages that previously floated via `Version="*"` (`Microsoft.AspNetCore.Mvc.Testing`, `Reqnroll.xUnit`, `Respawn`, `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq`) are now pinned to the version that was actually resolved at change time (`10.0.9` / `3.3.4` / `7.0.0` / `4.13.0` / `4.13.0`), so behavior is unchanged; future bumps go through this one file. Verified: `dotnet restore`/`build` clean, `scripts/ci-checks.sh full` green (6 unit + 13 architecture + 2/44 functional passing — rest are pre-existing `@ignore`d BDD scenarios, unrelated to this change).
- [x] **37. ✅ 2026-07-04** 裁決＝刪除 `general/GEMINI.md`（2 字元空殼，正式提交刪除後現實與 ADR-001 §3 inventory 一致，無需動 ADR 本文）。原描述：ADR-001 §3 `general/` inventory lists only `solid.rule.md`; `general/GEMINI.md` also exists. No functional conflict (CLAUDE.md §0 loads `general/*.rule.md`, GEMINI.md is excluded by suffix), pure inventory lag.
