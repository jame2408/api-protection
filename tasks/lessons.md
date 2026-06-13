# Lessons Learned

Patterns and lessons captured during development. Updated automatically per Self-Improvement Loop rules in CLAUDE.md.

## Trigger conditions
- User correction or pushback
- Self-correction after failed command or wrong approach
- Non-obvious technical decision (architecture, library choice, tradeoff)
- Non-trivial or surprising bug root cause
- Repeated issue (second occurrence)
- User confirms a non-obvious approach worked

---

<!-- Entries added below as they occur -->

### [decision] 「Service 必回 Result」架構規則應鎖定 `*Handler`，不是 `*Service`
**Date:** 2026-06-13
**Context:** 建 NetArchTest 防線時，原打算對所有 `*Service` 類別斷言「必回 `Result<T,Failure>`」。實查發現三個具體 `*Service` 全是跨 BC contract 實作或 infra：`AccessPolicyService` 實作 `SharedKernel.Contracts.IAccessPolicyService` 回 `Task<Guid>`、`ConsumerValidatorService` 實作 `IConsumerValidator` 回 `Task<ConsumerValidationResult>`、`ScopeRegistryService` 在 Infrastructure。naive 規則會誤紅這些合法程式碼——那是一條寫錯的檢驗，比沒有更糟。BC 內部真正的 use-case 單位是 `*Handler`（`CreateApiKeyHandler.HandleAsync` 回 `Result`）。
**Rule:** 「Service/Handler 必回 Result」的機械化檢驗鎖定 concrete `*Handler` 類別的 public async 方法；跨 BC contract（`SharedKernel/Contracts`，由 `*Service` 實作）依 exceptions.rule.md「跨 BC Contract 例外」豁免，自然繞開不需特例。寫架構檢驗前先實查目標型別的真實形狀，別照規則字面套。
**落地:** `backend/tests/Architecture.Tests/HandlerResultReturnTests.cs`（已綠＋故意紅驗證，點名 `CreateApiKeyHandler.HandleAsync`）。

### [info] NetArchTest 查不到方法回傳型別；FunctionalTests 需要 Docker
**Date:** 2026-06-13
**Context:** (1) NetArchTest 的 fluent API 只做 IL 級 dependency 檢查（BC 隔離可用），但無法斷言「方法回傳 `Result<,>`」；Repository/Handler 回傳型別規則改用 reflection 測試。(2) 全套件 `dotnet test` 本機跑會有 2 個 BDD 場景失敗，根因是 Testcontainers 需要 Docker（`DockerUnavailableException`），非迴歸——本機沒開 Docker 時無法驗證 BDD，但 GitHub Actions 的 ubuntu runner 內建 Docker 會綠。
**Rule:** 架構規則「dependency 用 NetArchTest、回傳型別用 reflection」分工；判斷「suite 是否 Green」要先排除 Docker/Testcontainers 這類環境因素，別誤判成迴歸。
**落地:** reflection 測試 `RepositoryReturnTypeTests.cs` / `HandlerResultReturnTests.cs`；CI `.github/workflows/ci.yml` 用 `ubuntu-latest`（Docker 預裝）並於註解說明。

### [decision] 架構規則依「檢驗對象在哪」選工具：型別圖 / 方法簽名 / 語法
**Date:** 2026-06-13
**Context:** 第二批四條規則各有最適工具：(1) BC 隔離 = 型別依賴圖 → NetArchTest（IL 級）；(2) Repository/Handler 回傳型別、ILogger 注入、命名 = 型別/成員 metadata → reflection；(3) `new Failure(`、`cancel` 參數命名 = **method body 內的建構式呼叫 / 參數名稱**，型別圖與 reflection 都看不到 → 只能 grep 原始碼。硬把語法層級規則塞進 NetArchTest/reflection 會寫不出來或寫錯。
**Rule:** 機械化一條規則前先問「違規長在哪個層次」：型別依賴→NetArchTest；型別/成員 metadata→reflection；method body/語法→grep（`scripts/source-lint.sh`）或 Roslyn analyzer。grep 的好處是 cheap，可放進 pre-commit fast 模式即時擋。命名命名豁免（如 `new Failure(` 的 `FailureProvider.cs`）必須在 lint 內明文排除，不是默契。
**落地:** `LoggerBoundaryTests.cs` / `NamingConventionTests.cs`（reflection）+ `scripts/source-lint.sh`（grep，接進 `ci-checks.sh` fast+full）。Architecture.Tests 3→11 tests。

### [decision] 寫的當下 PreToolUse hook 只攔「高信心、與下游一致」的 pattern，不攔 throw
**Date:** 2026-06-13
**Context:** 補最內層防線（編輯當下攔截）時，plan §B2 原列要攔 `new Failure(`/`throw`/`ILogger`/`ct` 四類。實查 `throw new` 在 src 的分布：`Result.cs` 存取器守衛、`InfrastructureModule.cs` 設定守衛、`IConsumerValidator` 參數驗證——全是合法 throw。文字層級攔 `throw` 會大量誤報，而誤報的 hook 比沒有更糟（訓練人/agent 忽略或關掉它）。
**Rule:** 寫的當下 hook 只攔「文字層級可零誤報、且已在 source-lint/架構測試強制」的 pattern（`new Failure(` 豁免 FailureProvider、bare-string code、`cancel` 命名、`ILogger<` 於 Service/Domain/Handler 路徑）。需要語意判斷或會誤報的（如 `throw`）留給 reflection 架構測試的結構性檢查，不放進文字 hook。hook 與 source-lint 共用同一組 pattern → 四層防線（寫/commit/push/CI）規則一致不漂移。
**落地:** `.claude/hooks/pre-tool-edit.py` + `.claude/settings.json` PreToolUse 註冊（matcher `Edit|Write|MultiEdit`）；exit 2 擋並回報。9 情境測試全對。

### [correction] 寫 production code 前必須主動載入 .claude/references 規則檔
**Date:** 2026-04-03
**Context:** Wave 1 初始實作時，CreateApiKeyHandler 用 throw 做業務邏輯、CancellationToken 命名 ct、ConsumerValidatorService 在 Scoped 服務內建多餘子 scope，三個問題都是因為沒有載入 .claude/references/dotnet/*.rule.md 就直接寫程式造成的。事後 code review 才全部補救。
**Rule:** 每次對這個 project 寫 production code（Handler、Service、Repository、Endpoint）前，必須先讀取 .claude/references/general/*.rule.md 和 .claude/references/dotnet/*.rule.md，確認再動手。核心規則：(1) Service 層用 Result<T,Failure> + FailureProvider.CreateFailure()，不 throw；(2) CancellationToken 參數一律命名 cancel；(3) Scoped 服務直接注入依賴，不用 IServiceScopeFactory.CreateScope()。
