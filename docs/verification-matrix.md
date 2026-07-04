# 驗證矩陣（Verification Matrix）

> **治理聲明（descriptive，非規範本體）**：本檔是一張**登記表**——記錄「哪條規則」「由什麼機制」「在什麼時機」「由誰（腳本／哪一級模型／人）」驗證。規則本體不在此複寫，一律以指針（ADR 章節 / rule 檔 / `CLAUDE.md` 段落）標示權威來源；本檔僅描述現況、不創設新規則（比照 `docs/adr/adr-007-process-governance.md` 規則 5 對 `docs/orchestration.md` / `AGENTS.md` 的要求）。
>
> **同步義務**：任何檢驗機制新增、修改、或除役（新增測試、改 lint pattern、換 hook 邏輯）時，本表必須在**同一個 commit** 內同步更新（`docs/adr/adr-007-process-governance.md` 規則 2：規範文件修改若影響其他文件對同一規則的引用，須同 commit 修正）。
>
> 關閉 `tasks/process-improvement-plan.md` §9.2 O-6。

---

## 主表

| # | 規則（一句話 + 權威來源指針） | 機制（確切檔名） | 時機層 | 執行者 |
|---|---|---|---|---|
| **Architecture.Tests — 13 個測試案例（6 個測試類，`BoundedContextIsolationTests` 為 5 筆資料的 Theory）** ||||
| 1 | BC 不得直接依賴其他 BC，只能透過 `SharedKernel/Contracts`（`CLAUDE.md` §Non-Negotiable Constraints「NEVER add direct BC-to-BC references」+ `docs/adr/adr-003-error-handling-and-cross-bc-contracts.md`） | `backend/tests/Architecture.Tests/BoundedContextIsolationTests.cs`（`[Theory]`，5 個 BC × 1 斷言 = 5 個測試案例） | push 前 / CI（`scripts/ci-checks.sh full`） | 腳本 |
| 2 | `Failure` 只能有 `Code` 一個公開成員，禁止加欄位（shape lock） — `docs/adr/adr-004-failure-shape-and-claude-md-alignment.md` §4 | `backend/tests/Architecture.Tests/FailureShapeTests.cs`（2 個 `[Fact]`） | push 前 / CI | 腳本 |
| 3 | BC 內 `*Handler` 的 public async 方法必回 `Task<Result<T,Failure>>`，不得為業務邏輯 `throw` — `CLAUDE.md` §4 Verification Standards「Service layer uses Result」+ `.claude/references/dotnet/exceptions.rule.md` §A | `backend/tests/Architecture.Tests/HandlerResultReturnTests.cs`（1 個 `[Fact]`） | push 前 / CI | 腳本 |
| 4 | `Service`/`Domain`/`*Handler` 不得注入 `ILogger` — `CLAUDE.md` §4「NEVER inject ILogger into Service, Domain, or Handler layers」+ `.claude/references/dotnet/di.rule.md` §F | `backend/tests/Architecture.Tests/LoggerBoundaryTests.cs`（1 個 `[Fact]`） | push 前 / CI | 腳本 |
| 5 | 命名慣例：實作 `I*Handler` 必須命名 `*Handler`；實作 `I*Repository` 必須命名 `*Repository`；`*FailureCodes` 必須是 `static class` 且只含 `const string` — `.claude/references/dotnet/naming.guide.md` §A + `.claude/references/dotnet/exceptions.rule.md` §E | `backend/tests/Architecture.Tests/NamingConventionTests.cs`（3 個 `[Fact]`） | push 前 / CI | 腳本 |
| 6 | `*Repository` 介面方法必須回傳原始型別，禁止回傳 `Result<T,Failure>` — `.claude/references/dotnet/exceptions.rule.md` §B | `backend/tests/Architecture.Tests/RepositoryReturnTypeTests.cs`（1 個 `[Fact]`） | push 前 / CI | 腳本 |
| **`scripts/source-lint.sh` — 逐 pattern（3 個，method body / 語法層級，NetArchTest 與 reflection 都看不到）** ||||
| 7 | 禁止 `new Failure(...)` 直接建構，一律經 `FailureProvider.CreateFailure()`（`FailureProvider.cs` 自身豁免） — `CLAUDE.md` §4「NEVER use `new Failure()`」 | `scripts/source-lint.sh`（`new_failure` 檢查段） | commit 前 / push 前 / CI | 腳本 |
| 8 | Failure code 不得為裸字串，須用 `*FailureCodes` 常數 — `.claude/references/dotnet/exceptions.rule.md` §E | `scripts/source-lint.sh`（`bare_code` 檢查段） | commit 前 / push 前 / CI | 腳本 |
| 9 | `CancellationToken` 參數必須命名 `cancel`，不得是 `cancellationToken` / `ct` — `CLAUDE.md` §4「Naming conventions」+ `.claude/references/dotnet/naming.guide.md` §B | `scripts/source-lint.sh`（`bad_cancel` 檢查段） | commit 前 / push 前 / CI | 腳本 |
| **`scripts/adr-lint.sh` — 結構性（1 項，涵蓋 7 個子檢查：Status 格式 / 7 個必要章節 / governance clause / 禁 file:line / 檔名編號連續 / Alternative 需 `Rejected.` / Trade-off 需 `Mitigation:`）** ||||
| 10 | `docs/adr/adr-*.md` 結構性合規 — `CLAUDE.md`「Architecture Decision Records (ADR)」段 + 其「Validation」→「Structural lint (mechanical)」子段 | `scripts/adr-lint.sh` | commit 前（僅當 `docs/adr/` 有 staged 變更才觸發，見 `scripts/git-hooks/pre-commit`）/ push 前 / CI | 腳本 |
| **`dotnet format`** ||||
| 11 | C# 原始碼格式一致性（縮排、`using` 排序等 .NET 預設格式規則；repo 未建 `.editorconfig` 客製化，套用 `dotnet format` 內建規則集——**權威來源模糊**，非對應特定 `CLAUDE.md` 條文） | `scripts/ci-checks.sh` `format_check()` → `dotnet format backend/ApiKeyManagement.slnx --verify-no-changes` | commit 前 / push 前 / CI | 腳本 |
| **`.claude/hooks/pre-tool-edit.py` — 寫時攔截，4 個 pattern（僅 Claude Code harness 有效；其他 harness 對策見 `AGENTS.md`「此 harness 拿不到的防線」段）** ||||
| 12 | 同第 7 項（`new Failure(` 攔截，寫的當下） | `.claude/hooks/pre-tool-edit.py`（`new\s+Failure\s*\(` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本（hook，exit 2 阻擋） |
| 13 | 同第 8 項（bare-string `CreateFailure("..."` 攔截，寫的當下） | `.claude/hooks/pre-tool-edit.py`（`CreateFailure\("` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本 |
| 14 | 同第 9 項（`CancellationToken` 命名攔截，寫的當下） | `.claude/hooks/pre-tool-edit.py`（`cancellationToken\|ct` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本 |
| 15 | 同第 4 項（`Domain`/`Application`/`*Handler` 注入 `ILogger` 攔截，寫的當下；刻意不攔 `throw`——合法 guard throw 會誤報，見 `tasks/lessons.md` 2026-06-13 [decision]） | `.claude/hooks/pre-tool-edit.py`（`ILogger\s*<` regex 段，限 `in_logger_zone`） | 寫的當下（**限 Claude Code harness**） | 腳本 |
| **`scripts/hook-smoke.sh`（`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`，與本表同批落地）** ||||
| 16 | `session-init.sh` 注入邏輯必須可測：(a) 新 `session_id` → 注入 must-read + `tasks/lessons.md` 最近 8 條；(b) 同 `session_id` 二次呼叫 → 不重複注入；(c) 缺 `session_id` → 保守仍注入，不誤判為已注入 — `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` Implementation Rules 1 / 2 / 4 | `scripts/hook-smoke.sh`（`scripts/ci-checks.sh` fast 與 full 皆呼叫，維持「fast ⊂ full」不變式） | commit 前 / push 前 / CI | 腳本 |
| **BDD FunctionalTests（wire contract 鎖定）** ||||
| 17 | `CreateApiKey` 失敗回應必須符合 RFC 9457 Problem Details（`status` / `errorCode` / `title` / `type` / `traceId` + `Content-Type: application/problem+json`），鎖住所有使用此 step 的場景（含 `@ignore`） — `docs/design/api-spec.md` §2.2 | `backend/tests/FunctionalTests/Steps/CreateApiKeySteps.cs` `ThenCreateFailsWithReason` | push 前 / CI（需 Docker／Testcontainers，屬 `ci-checks.sh full` 的 `build_and_test` 段） | 腳本 |
| 18 | `CreateApiKey` 成功回應必須含 `truncatedKey`（`"..." +` 明碼末 4 碼） — `docs/design/api-spec.md` §2.2 | `backend/tests/FunctionalTests/Steps/CreateApiKeySteps.cs` `ThenRawKeyIsReturned` | push 前 / CI | 腳本 |
| **SharedKernel.Tests** ||||
| 19 | `FailureProvider.CreateFailure()` 是建構 `Failure` 的唯一合法入口：合法 code 忠實回填 `Failure.Code`；`null`／空白／空字串 code 必須丟 `ArgumentException` — `CLAUDE.md` §4「NEVER use `new Failure()`」的配套單元測試 | `backend/tests/SharedKernel.Tests/Domain/FailureProviderTests.cs`（1 個 `[Fact]` + 1 個 `[Theory]`×5 = 6 個測試案例） | push 前 / CI | 腳本 |
| **AI review 類** ||||
| 20 | Code review：bug 偵測、安全性稽核、依賴影響分析 | `.claude/skills/code-review/SKILL.md`（PR mode / Self mode） | review 時 | 中型模型 |
| 21 | Orchestrator review executor 產出：事實覆核（不接受概括摘要）、誠實申報覆核、**含簡體字元掃描**（見 `tasks/lessons.md` 2026-07-04 [correction]） — `docs/orchestration.md` §2 Executor Contract 第 3 條「誠實申報 blocker」的覆核方 | 無獨立腳本檔——純人工/大型模型執行的 review 步驟，權威來源見 `docs/orchestration.md` §2 與 `tasks/lessons.md` 對應條目；⚠️ 簡體字掃描本身**無腳本化**（見下方無防線區塊） | review 時 | 大型模型 |
| **人工類** ||||
| 22 | ADR PR review checklist（7 項 judgment 檢查：Context 並排引用 / Decision 邊界 / code 範例 / Rationale 三問 / ≥3 Alternatives / Implementation Rules 可打勾 / 同步項目同 commit） — `CLAUDE.md`「Architecture Decision Records (ADR)」→「Validation」→「Review checklist (judgment, not mechanical)」子段 | 無腳本；檢查清單本體見 `CLAUDE.md` 該段文字，本表僅放指針 | review 時 | 人 |

不列 Tessl（`tasks/process-improvement-plan.md` §9.3 D-2 裁決：擱置，不入制度）。

---

## 無防線區塊（規則存在但查無機械化檢驗，⚠️）

以下規則在 `CLAUDE.md` / ADR 中明文存在，但實查 `scripts/`、`.claude/hooks/`、`backend/tests/Architecture.Tests/` 後找不到對應機械化檢驗；照實標注，不假裝有防線。

| 規則 | 權威來源 | 追蹤狀態 |
|---|---|---|
| Unit test coverage ≥ 80%（Handler code） | `CLAUDE.md` §4「_Tests:_」 | 未追蹤——`coverlet.collector` 僅作為測試 SDK 依賴出現於 `.csproj`，`scripts/ci-checks.sh` / `.github/workflows/ci.yml` 均無涵蓋率門檻或報表解析步驟 |
| 每個 Guard condition 須同時有正向與負向情境 | `CLAUDE.md` §4「_Tests:_」 | 未追蹤——屬 BDD 撰寫完整性，無腳本比對 `.feature` 場景的正/負向覆蓋 |
| `NEVER` 存取 `.Value` 前未先檢查 `.IsFailure` | `CLAUDE.md` §4「_Error Handling_」 | 未追蹤——需資料流/Roslyn analyzer 才可靜態偵測，`backend/tests/Architecture.Tests/` 未見對應測試 |
| `NEVER` 使用空 catch block；`NEVER` 用 `throw ex;`（須 `throw;`） | `CLAUDE.md` §4「_Error Handling_」 | 未追蹤——`scripts/source-lint.sh` 未涵蓋，可用 grep 機械化但尚未寫 |
| `CancellationToken cancel` 須傳播到每個 I/O 呼叫（EF Core / HTTP client / message bus） | `CLAUDE.md` §4「_Code Quality_」 | 未追蹤——命名本身已由第 9 / 14 項機械化，但「有沒有把 `cancel` 真的傳進每個 I/O 呼叫」屬語意/資料流檢查，現有 grep 與 reflection 皆看不到 |
| 命名慣例：一般 PascalCase 方法 / `_camelCase` 欄位 / `Async` 後綴 | `CLAUDE.md` §4「_Code Quality_」 | 未追蹤——`NamingConventionTests.cs` 只鎖 `*Handler`/`*Repository`/`*FailureCodes` 後綴這三類，未涵蓋一般方法/欄位命名；repo 無 `.editorconfig`、無 analyzer 設定 |
| FluentAssertions 用於測試斷言，禁止直接比較（如 `Assert.Equal`） | `CLAUDE.md` §4「_Code Quality_」 | 未追蹤——無 lint 禁止 `Assert.*`；現況所有測試皆用 FluentAssertions 純屬慣例延續，非機械保證 |
| API Key validation latency P99 < 50ms | `CLAUDE.md` §4「_Performance (for hotpath changes)_」 | 未追蹤——repo 內無負載測試腳本或效能基準測試 |
| Validation throughput ≥ 100 RPS | `CLAUDE.md` §4「_Performance (for hotpath changes)_」 | 未追蹤——同上，無對應腳本 |
| 禁止簡體字（正體中文文件） | 全域層級規則（`~/.claude/CLAUDE.md`，不在本 repo 內），本 repo 內無明文條文、無 lint | 已追蹤——`tasks/process-improvement-plan.md` §8.3「禁簡體無機械化防線」+ §9.4 Phase D 殘項；現況僅靠 orchestrator review 人工掃描（見主表第 21 項），機械化需先開 ADR 裁決規範落點 |

---

## 審校紀錄（2026-07-04 orchestrator review）

- Executor 自查時回報的 4 項並行時序落差（hook-smoke 未接線、`pending-lessons.jsonl` 未刪、`.gitignore` 缺 `.claude/*.marker`、第 16 項為預寫狀態），於本表 commit 時**已全部消解**：Phase B 落地 + orchestrator 裁決 `hook_smoke` 同時接入 fast 與 full（維持「fast ⊂ full」不變式）。
- **仍開放**：`dotnet format` 的權威來源模糊（主表第 11 項）——repo 無 `.editorconfig`，格式規則對應不到任何 `CLAUDE.md`/ADR 條文，僅為工具預設。是否補 `.editorconfig` 正式化，追蹤於 `tasks/process-improvement-plan.md` §8.3。
- 主表其餘各行「機制」檔案路徑已由 executor 逐一 `ls`/`grep` 確認存在（見下方核對清單），orchestrator 抽驗無誤。

---

## 自查核對清單（機制檔案路徑存在性）

```
OK   backend/tests/Architecture.Tests/BoundedContextIsolationTests.cs
OK   backend/tests/Architecture.Tests/FailureShapeTests.cs
OK   backend/tests/Architecture.Tests/HandlerResultReturnTests.cs
OK   backend/tests/Architecture.Tests/LoggerBoundaryTests.cs
OK   backend/tests/Architecture.Tests/NamingConventionTests.cs
OK   backend/tests/Architecture.Tests/RepositoryReturnTypeTests.cs
OK   scripts/source-lint.sh
OK   scripts/adr-lint.sh
OK   scripts/ci-checks.sh
OK   .claude/hooks/pre-tool-edit.py
OK   backend/tests/FunctionalTests/Steps/CreateApiKeySteps.cs
OK   backend/tests/SharedKernel.Tests/Domain/FailureProviderTests.cs
OK   .claude/skills/code-review/SKILL.md
OK   docs/orchestration.md
OK   docs/adr/adr-007-process-governance.md
OK   docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md
OK   docs/design/api-spec.md
OK   CLAUDE.md
OK   AGENTS.md
OK   scripts/git-hooks/pre-commit
OK   scripts/git-hooks/pre-push
OK   .github/workflows/ci.yml
OK   scripts/hook-smoke.sh   (已接入 ci-checks.sh fast + full——見審校紀錄)
```
