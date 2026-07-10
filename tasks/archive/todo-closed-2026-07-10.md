# todo.md 結案歸檔（2026-07-10 歸檔 pass）

> 自 `tasks/todo.md`（原檔頭「`.claude/` Hardening — Outstanding Work」）移出的已結案內容，逐字保留。開放項與原編號仍在 `tasks/todo.md`；ADR 或 checkpoint 引用的舊編號若不在 todo.md，即在本檔。

## `.claude/` Hardening — Done（7-commit plan，multi-session hardening pass，最後 commit 2026-04-30 `8922c47`）

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

## ADR-004 acceptance (staged, 2026-05-01)

- [x] ADR-004 Status `Proposed` → `Accepted (2026-05-01)`.
- [x] ADR-004 §4: rewrote NetArchTest snippet as illustrative xUnit reflection test; noted NetArchTest fluent API has no `NotHavePropertyOtherThan(...)`.
- [x] ADR-004 Implementation Rules: added rule that `FailureProvider.CreateFailure` rejects null / whitespace code.
- [x] CLAUDE.md:93 replaced with ADR-004 §3 wording (Handler added to no-ILogger list; removed "embed into Failure message or metadata"; added pointer to ADR-004).
- [x] `FailureProvider.CreateFailure` adds `ArgumentException.ThrowIfNullOrWhiteSpace(code)`.
- [x] New `backend/tests/SharedKernel.Tests/` project with `FailureProviderTests`: valid code returns Failure (1) + null/empty/whitespace/tab/newline throws (5) → 6/6 Green.
- [x] `SharedKernel.Tests` pinned to `FluentAssertions 7.2.0` to comply with `testing.guide.md` (`< 8.0.0`); the FunctionalTests violation was tracked separately as multi-agent review item #19（後由 #36 CPM 統一解決）.
- [x] `ApiKeyManagement.slnx` includes the new test project.
- [x] todo.md cross-doc consistency sweep: (a) removed obsolete "CLAUDE.md still says diagnostic context should be embedded" follow-up; (b) marked multi-agent review item #10 resolved with pointer to ADR-004; (c) item #5 + action-sequence #3 no longer mis-route the hash-algorithm decision through ADR-004.
- [x] `dotnet build` + `dotnet test` Green; `dotnet format --verify-no-changes` clean.

Out of scope at the time (均已後續落地): Architecture.Tests Failure-shape rule（#20 已落地）; boundary structured logging（ADR-004 review 明文「不急」）; ADR-005/ADR-006 acceptance（已 Accepted）。

## 已結案的 follow-ups（自 Non-blocking 段移出）

- ~~`CreateApiKeyFailureCodes.ValidationErrorPrefix` 收緊~~ — 與 review item #4 同一件事，收斂到 #4 追蹤（仍開放，見 todo.md）。
- ~~`Architecture.Tests` discovers 0 tests~~ — 已由 #20 落地解決（14 tests）。
- ~~`IServiceScopeFactory` / `FailureProvider` / `IDbContextFactory` cleanup deferred from earlier round~~ — `FailureProvider` 部分由 ADR-004 acceptance 處理（null/whitespace guard）；`CreateScope`/`IServiceScopeFactory` 已由 source-lint 9c 禁令承接（`backend/src/` 禁用，Middleware/Program.cs 豁免）；如仍有 `IDbContextFactory` 殘餘再重開。

## Working principles confirmed in the hardening pass

- Production design is fixed first, **then** references — never the reverse.
- Per-BC failure code constants live with the BC; cross-BC contract codes live next to the contract; no preemptive Common class.
- API contract / wire-format tests intentionally use string literals to lock external behavior (production code uses constants).
- `record Failure(string Code)` — single field; no message/metadata overload exists.

## Multi-agent review (2026-04-30) — 已結案項

### Cross-doc consistency sweep (2026-05-31) 狀態複核

- ✅ Newly confirmed resolved since the review: **#12** (`code-review/SKILL.md` nodejs/python table — 0 hits), **#13** (`settings.local.json` `dotnet format` pattern — 0 hits), **#11** (resolved-by-decision: ADR-005 scoped test fixtures out + added `testing.guide.md:46` caveat).
- Decision-chain semantics verified **clean** (ADR-001/002/003/004/005/006 all hold against code: enum PascalCase 0-hit, `Failure` single-field, no MediatR, no Service/Domain/Handler `ILogger`, ADR-005 §6 sync comments landed, "121 scenarios" = 44+15+13+30+19 confirmed exact).

### 已結案清單

1. ~~**Endpoint error responses don't follow RFC 9457 ProblemDetails.**~~ ✅ Resolved（2026-07-05 盤查確認，登記滯後補記）— `backend/src/KeyLifecycle/Http/ApiProblem.cs` 為單一 error envelope 來源（`type`/`title`/`status`/`errorCode`/`traceId` + `application/problem+json`），BDD `ThenCreateFailsWithReason` 逐欄鎖定 wire format（`docs/verification-matrix.md` 第 17 項）。
2. ~~**`CreateApiKeyResponse.cs` missing `truncatedKey` field.**~~ ✅ Resolved（2026-07-05 盤查確認）— `CreateApiKeyResponse` 已含 `TruncatedKey`，`ThenRawKeyIsReturned` 斷言 `"..." + rawKey[^4..]`（`docs/verification-matrix.md` 第 18 項）。
3. ~~**`lifecycleStatus` wire-format inconsistency.**~~ ✅ Resolved by ADR-006 (2026-05-02)：`ApiKeyStatus` enum 改 PascalCase + `JsonStringEnumConverter(allowIntegerValues: false)`；DTO 型別改 `ApiKeyStatus`；functional tests 以 `JsonDocument` 鎖 wire literal `"Active"`。
5. ~~**Hash algorithm violates `prd.md` §5.2 + R-SEC-03.**~~ ✅ 已裁決 2026-07-05（ADR-017：HMAC-SHA256 + pepper，實作同 commit 落地 `abc71aa`）。
6. ~~**`BCrypt.Net-Next` version mismatch.**~~ ✅ Resolved (2026-07-04) via #36 — CPM 統一 pin `4.1.0`（後由 ADR-017 全數移除 BCrypt 依賴）。
9. ~~**Dev `appsettings.Development.json` commits a plaintext DB password.**~~ ✅ Resolved (2026-07-04) — `Password=` 段移除，Host 加 `<UserSecretsId>`，本機設定指令入 root `README.md`。使用者裁決：不重寫 git 歷史（localhost postgres/postgres 為通用預設值，無保密價值）。
10. ~~**`Failure(string Code)` vs `CLAUDE.md:93` wording contradiction.**~~ ✅ Resolved by ADR-004 acceptance (2026-05-01)。
19. ~~**`FluentAssertions 8.9.0` license non-compliance.**~~ ✅ Resolved (2026-07-04) via #36 — CPM 統一 pin `7.2.0`。
20. ~~**`Architecture.Tests` is a stub.**~~ ✅ 全數落地：MVP 三條（2026-06-13，NetArchTest.Rules 1.3.2 + FluentAssertions 7.2.0）＋第二批（BC-isolation／Repository 回型／Handler Result／ILogger 邊界／命名慣例／`scripts/source-lint.sh` 語法層），11 tests 各「綠＋故意紅」；後續 BC 名單改動態發現（`a4094b3`，14 tests）。
35. ~~`tasks/bdd-progress.md` residual `ROTATING` → `Rotating`~~ ✅ 2026-07-04 已修正。
36. ~~套件版本中央管理~~ ✅ Resolved (2026-07-04)：`backend/Directory.Packages.props`（`ManagePackageVersionsCentrally=true`），12 個 `.csproj` 全數收編；原 floating `Version="*"` 套件 pin 至當時解析版（`10.0.9`/`3.3.4`/`7.0.0`/`4.13.0`/`4.13.0`）。`dotnet restore`/`build` clean、`ci-checks.sh full` 綠。
37. ~~ADR-001 §3 inventory lag（`general/GEMINI.md`）~~ ✅ 2026-07-04 裁決＝刪除 GEMINI.md（2 字元空殼），現實與 ADR-001 §3 一致。
39. ~~CLAUDE.md DoD「Architecture tests pass」gate 空轉~~ ✅ 2026-07-05 已隨 #20 落地自動消解（14 tests，gate 不再空轉）。

### Recommended action sequence（歷史版，多數已執行）

1. **Now** — items #6, #9, #11, #12, #13（已全數結案）。
2. **Before next BC wave** — #1, #2, #3, #4 wire-format alignment（#1–#3 已結案，#4 仍開放）; #20 Architecture.Tests seed（已結案）。
3. **Before validation hot-path** — #5 hash ADR（已結案 ADR-017）, #7 concurrency guard（開放）, #8 constant-time compare（開放，ADR-017 Rule 6 已固化義務）。
4. **Before any release** — #19 FluentAssertions license（已結案）。
5. **Opportunistic** — #10（已結案）, #14–#18, #21–#24（開放）。

## Coverage gate（QA 強化 #1）— 已落地（ADR-014，`e94a381`）

**目標**：機械化 DoD「unit coverage ≥ 80% for Handler code」。
**基線實測（2026-07-05，4/44 場景時點）**：`CreateApiKeyHandler` 89.1%、`ConsumerValidatorService` 100% → 80% 門檻上線即綠，隨場景解鎖單調上升，無需 ratchet。
**設計裁決（載於 ADR-014）**：度量 = 全測試套件含 BDD；鎖定 concrete `*Handler` 類（async state machine 併回母類），每類各自 ≥ 80%；時機 = full gate；機制 = coverlet.collector + `scripts/coverage-check.sh`。
**Alternatives（ADR 詳述）**：coverlet.msbuild `/p:Threshold`（assembly 級，Rejected）；ReportGenerator（新增依賴，Rejected）；unit-test-only 度量（違 BDD-first，Rejected）。
- [x] orchestrator 起草 `docs/adr/adr-014-handler-coverage-gate.md`。
- [x] executor：`scripts/coverage-check.sh` + full 接線 + 矩陣同 commit + 綠＋故意紅（門檻暫調 95 證紅後還原）。

## 團隊尺度共享檔 + BDD 需求分流（規劃 2026-07-06）— 已全數落地（ADR-021 `1a7e5f3`／ADR-022 `71a193c`）

> 兩案皆為使用者主動提出的需求（非投機性立法）。既往 ADR grep 反查完畢：無同案裁決。規劃內容（寫入模式分類表、六類需求分流表、`Spec-change:` trailer 機械化設計、執行編排）已全數轉載至兩份 Accepted ADR — 權威內容見 `docs/adr/adr-021-shared-state-files-team-scale.md` 與 `docs/adr/adr-022-bdd-requirement-type-routing.md`，此處不再複寫規劃原文。執行紀錄：引用掃描（haiku）→ 雙 ADR 起草過 `adr-lint.sh` → Executor A（lessons 拆檔＋session-init glob）→ Executor B（bdd-lint／pre-commit／commit-msg 擴充）串行，orchestrator 親驗放行。
