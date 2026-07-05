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
| **Architecture.Tests — 14 個測試案例（6 個測試類，`BoundedContextIsolationTests` 為動態發現的 Theory + 1 個 guard `[Fact]`）** ||||
| 1 | BC 不得直接依賴其他 BC，只能透過 `SharedKernel/Contracts`（`CLAUDE.md` §Non-Negotiable Constraints「NEVER add direct BC-to-BC references」+ `docs/adr/adr-003-error-handling-and-cross-bc-contracts.md`） | `backend/tests/Architecture.Tests/BoundedContextIsolationTests.cs`（`[Theory]`，BC 名單動態發現：檔案系統掃描 `backend/src/`，現況 5 BC = 5 個測試案例；新 BC 自動納管，漏加 ProjectReference 時 `Assembly.Load` fail-loud。另 1 個 guard `[Fact]` 鎖住已知最小 BC 集合，防發現邏輯靜默失效） | push 前 / CI（`scripts/ci-checks.sh full`） | 腳本 |
| 2 | `Failure` 只能有 `Code` 一個公開成員，禁止加欄位（shape lock） — `docs/adr/adr-004-failure-shape-and-claude-md-alignment.md` §4 | `backend/tests/Architecture.Tests/FailureShapeTests.cs`（2 個 `[Fact]`） | push 前 / CI | 腳本 |
| 3 | BC 內 `*Handler` 的 public async 方法必回 `Task<Result<T,Failure>>`，不得為業務邏輯 `throw` — `CLAUDE.md` §4 Verification Standards「Result-only in the service layer」+ `.claude/references/dotnet/exceptions.rule.md` §A | `backend/tests/Architecture.Tests/HandlerResultReturnTests.cs`（1 個 `[Fact]`） | push 前 / CI | 腳本 |
| 4 | `Service`/`Domain`/`*Handler` 不得注入 `ILogger` — `CLAUDE.md` §4「no \`ILogger\` in Service/Domain/Handler」+ `.claude/references/dotnet/di.rule.md` §F | `backend/tests/Architecture.Tests/LoggerBoundaryTests.cs`（1 個 `[Fact]`） | push 前 / CI | 腳本 |
| 5 | 命名慣例：實作 `I*Handler` 必須命名 `*Handler`；實作 `I*Repository` 必須命名 `*Repository`；`*FailureCodes` 必須是 `static class` 且只含 `const string` — `.claude/references/dotnet/naming.guide.md` §A + `.claude/references/dotnet/exceptions.rule.md` §E | `backend/tests/Architecture.Tests/NamingConventionTests.cs`（3 個 `[Fact]`） | push 前 / CI | 腳本 |
| 6 | `*Repository` 介面方法必須回傳原始型別，禁止回傳 `Result<T,Failure>` — `.claude/references/dotnet/exceptions.rule.md` §B | `backend/tests/Architecture.Tests/RepositoryReturnTypeTests.cs`（1 個 `[Fact]`） | push 前 / CI | 腳本 |
| **`scripts/source-lint.sh` — 逐 pattern（3 個，method body / 語法層級，NetArchTest 與 reflection 都看不到）** ||||
| 7 | 禁止 `new Failure(...)` 直接建構，一律經 `FailureProvider.CreateFailure()`（`FailureProvider.cs` 自身豁免） — `CLAUDE.md` §4「never \`new Failure()\`」 | `scripts/source-lint.sh`（`new_failure` 檢查段） | commit 前 / push 前 / CI | 腳本 |
| 8 | Failure code 不得為裸字串，須用 `*FailureCodes` 常數 — `.claude/references/dotnet/exceptions.rule.md` §E | `scripts/source-lint.sh`（`bare_code` 檢查段） | commit 前 / push 前 / CI | 腳本 |
| 9 | `CancellationToken` 參數必須命名 `cancel`，不得是 `cancellationToken` / `ct` — `CLAUDE.md` §4「CancellationToken cancel」propagated to every I/O call + `.claude/references/dotnet/naming.guide.md` §B | `scripts/source-lint.sh`（`bad_cancel` 檢查段） | commit 前 / push 前 / CI | 腳本 |
| 9a | MSBuild `.props`/`.targets` 檔須為合法 XML（XML 註解含 `--` 會使整檔被 MSBuild 靜默跳過，NU1015） — `tasks/lessons.md` Archived「Directory.Packages.props 內 XML 註解含 `--` 會被 MSBuild 靜默跳過（NU1015）」 | `scripts/source-lint.sh`（`bad_xml` 檢查段，對 `git ls-files '*.props' '*.targets'` 逐檔跑 `xml.dom.minidom.parse`） | commit 前 / push 前 / CI | 腳本 |
| 9b | repo 腳本（`scripts/*.sh`、`.claude/hooks/*.sh`）須維持 bash 3.2 相容，禁 `mapfile`/`readarray`/`trap ... RETURN` — `tasks/lessons.md` Archived「本機 bash 是 macOS 內建 3.2」 | `scripts/source-lint.sh`（`bash_compat` 檢查段，自身排除） | commit 前 / push 前 / CI | 腳本 |
| 9c | `backend/src/` 內禁止 `IServiceScopeFactory` / `.CreateScope(`，唯 `*Middleware.cs` 與 `Program.cs` 豁免（Scoped 服務須直接注入依賴，CreateScope 僅限 Singleton Middleware 避免 captive dependency） — `.claude/references/dotnet/di.rule.md` §C + `tasks/lessons.md` Archived「寫 production code 前必須主動載入 .claude/references 規則檔」 | `scripts/source-lint.sh`（`bad_scope` 檢查段） | commit 前 / push 前 / CI | 腳本 |
| **`scripts/bdd-lint.sh` + pre-commit staged 檢查（BDD 佇列紀律，`CLAUDE.md` §Non-Negotiable + §BDD Constraints）** ||||
| 9d | 一次只移除一個 `@ignore`；「identical new step definitions」例外以 `ALLOW_MULTI_IGNORE=1` 豁免 — `CLAUDE.md` §Non-Negotiable Constraints | `scripts/git-hooks/pre-commit`（staged diff 計數 guard） | commit 前（僅本機 hook；`--no-verify` 可繞過，CI 不覆蓋——誠實標注，殘餘風險由 9e 帳面檢查部分回補） | 腳本 |
| 9e | `tasks/bdd-progress.md` 與實作同 commit + 帳面一致（已通過 N/M 必等於 場景總數−`@ignore` 數） — `CLAUDE.md` §BDD「same commit」條 | `scripts/git-hooks/pre-commit`（同 commit staged 檢查）+ `scripts/bdd-lint.sh`（帳面一致性） | commit 前 / push 前 / CI | 腳本 |
| 9f | scenario enablement commit 必附重構判斷 trailer——`.claude/skills/bdd-vertical-slice/SKILL.md` 步驟 9 的判斷（含「不重構」）不得省略留痕 — `CLAUDE.md` §BDD Constraints「Refactor-assessment」條 | `scripts/git-hooks/commit-msg`（staged net `@ignore` 移除 ≥ 1 時強制 message 含 `Refactor-assessment: .+`） | commit 前（僅本機 hook；`--no-verify` 可繞過，CI 不覆蓋——誠實標注，殘餘風險比照 9d） | 腳本 |
| **`scripts/adr-lint.sh` — 結構性（1 項，涵蓋 7 個子檢查：Status 格式 / 7 個必要章節 / governance clause / 禁 file:line / 檔名編號連續 / Alternative 需 `Rejected.` / Trade-off 需 `Mitigation:`）** ||||
| 10 | `docs/adr/adr-*.md` 結構性合規 — `CLAUDE.md`「Architecture Decision Records (ADR)」段 + 其「Validation」→「Structural lint (mechanical)」子段 | `scripts/adr-lint.sh` | commit 前（僅當 `docs/adr/` 有 staged 變更才觸發，見 `scripts/git-hooks/pre-commit`）/ push 前 / CI | 腳本 |
| **`dotnet format`** ||||
| 11 | C# 原始碼格式一致性（縮排、`using` 排序等 .NET 預設 whitespace 規則，權威來源仍為工具預設）+ 命名慣例（方法/屬性/型別/事件 PascalCase、私有欄位 `_camelCase`、介面 `I` 前綴、async 方法 `Async` 後綴——`backend/tests` 依 ADR-011 §3 排除 Async 後綴 carve-out）— `docs/adr/adr-011-naming-rules-editorconfig-enforcement.md` + `.claude/references/dotnet/naming.guide.md`（規則細節權威來源） | `backend/.editorconfig`（`dotnet_naming_*` + `dotnet_diagnostic.IDE1006.severity=error`）+ `backend/Directory.Build.props`（`EnforceCodeStyleInBuild=true`）；命名違規在 `dotnet build backend/ApiKeyManagement.slnx` 與 `scripts/ci-checks.sh` `format_check()`（`dotnet format --verify-no-changes`）皆會擋下 | commit 前 / push 前 / CI | 腳本 |
| **`.claude/hooks/pre-tool-edit.py` — 寫時攔截，4 個 pattern（僅 Claude Code harness 有效；其他 harness 對策見 `AGENTS.md`「此 harness 拿不到的防線」段）** ||||
| 12 | 同第 7 項（`new Failure(` 攔截，寫的當下） | `.claude/hooks/pre-tool-edit.py`（`new\s+Failure\s*\(` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本（hook，exit 2 阻擋） |
| 13 | 同第 8 項（bare-string `CreateFailure("..."` 攔截，寫的當下） | `.claude/hooks/pre-tool-edit.py`（`CreateFailure\("` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本 |
| 14 | 同第 9 項（`CancellationToken` 命名攔截，寫的當下） | `.claude/hooks/pre-tool-edit.py`（`cancellationToken\|ct` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本 |
| 15 | 同第 4 項（`Domain`/`Application`/`*Handler` 注入 `ILogger` 攔截，寫的當下；刻意不攔 `throw`——合法 guard throw 會誤報，見 `tasks/lessons.md` 2026-06-13 [decision]） | `.claude/hooks/pre-tool-edit.py`（`ILogger\s*<` regex 段，限 `in_logger_zone`） | 寫的當下（**限 Claude Code harness**） | 腳本 |
| **`.claude/hooks/post-edit-validate.sh`（`docs/adr/adr-012-charter-amendments-external-adoption.md`，P1，僅 Claude Code harness 有效）** ||||
| 15a | 寫後語法驗證：`.sh`→`bash -n`、`.json`→JSON parse、`.py`→`py_compile`、`.props`/`.csproj`/`.targets`/`.xml`→拒絕 `<!DOCTYPE`/`<!ENTITY` 後以 `xml.etree.ElementTree.parse` 驗 well-formed；直接對應本專案 NU1015 事故（XML 註解 `--` 靜默破壞 `.props`） | `.claude/hooks/post-edit-validate.sh`（PostToolUse，matcher `Edit\|Write`） | 寫的當下（**限 Claude Code harness**） | 腳本（hook，exit 2 阻擋） |
| **`scripts/machinery-check.sh`（`docs/adr/adr-012-charter-amendments-external-adoption.md`，P2，治理機械本身的自體健檢）** ||||
| 15b | settings.json / `.mcp.json` JSON 合法性、settings.json hooks 段引用腳本存在＋可執行＋語法通過、`.claude/hooks/*.sh` 與 `scripts/*.sh` 全數 `bash -n`、`CLAUDE.md`/`docs/orchestration.md`/`docs/verification-matrix.md` 反引號路徑指針完整性（fail-loud，無 `if [ -f ]` 靜默跳過；豁免限三類：`machinery-check:ignore` 行內標記、含 `*` 的 glob、gitignored 路徑——機器本地檔如 settings.local.json 在 CI checkout 本來就不存在，非 drift 訊號） | `scripts/machinery-check.sh`（`scripts/ci-checks.sh` fast 與 full 皆呼叫） | commit 前 / push 前 / CI | 腳本 |
| **`scripts/hook-smoke.sh`（`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`，與本表同批落地）** ||||
| 16 | `session-init.sh` 注入邏輯必須可測：(a) 新 `session_id` → 注入 must-read + `tasks/lessons.md` 最近 8 條；(b) 同 `session_id` 二次呼叫 → 不重複注入；(c) 缺 `session_id` → 保守仍注入，不誤判為已注入 — `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` Implementation Rules 1 / 2 / 4 | `scripts/hook-smoke.sh`（`scripts/ci-checks.sh` fast 與 full 皆呼叫，維持「fast ⊂ full」不變式） | commit 前 / push 前 / CI | 腳本 |
| **`scripts/zh-lint.sh`（`docs/adr/adr-009-traditional-chinese-and-zh-lint.md`）** ||||
| 16a | repo 內 tracked 檔案的中文一律正體；簡體字元攔截（OpenCC 字表 vendored + variant 白名單 + `zh-lint:allow` 行內豁免） — `docs/adr/adr-009-traditional-chinese-and-zh-lint.md` | `scripts/zh-lint.sh` + `scripts/data/opencc-STCharacters.txt`（`scripts/ci-checks.sh` fast 與 full 皆呼叫） | commit 前 / push 前 / CI | 腳本 |
| **BDD FunctionalTests（wire contract 鎖定）** ||||
| 17 | `CreateApiKey` 失敗回應必須符合 RFC 9457 Problem Details（`status` / `errorCode` / `title` / `type` / `traceId` + `Content-Type: application/problem+json`），鎖住所有使用此 step 的場景（含 `@ignore`） — `docs/design/api-spec.md` §2.2 | `backend/tests/FunctionalTests/Steps/CreateApiKeySteps.cs` `ThenCreateFailsWithReason` | push 前 / CI（需 Docker／Testcontainers，屬 `ci-checks.sh full` 的 `build_and_test` 段） | 腳本 |
| 18 | `CreateApiKey` 成功回應必須含 `truncatedKey`（`"..." +` 明碼末 4 碼） — `docs/design/api-spec.md` §2.2 | `backend/tests/FunctionalTests/Steps/CreateApiKeySteps.cs` `ThenRawKeyIsReturned` | push 前 / CI | 腳本 |
| **SharedKernel.Tests** ||||
| 19 | `FailureProvider.CreateFailure()` 是建構 `Failure` 的唯一合法入口：合法 code 忠實回填 `Failure.Code`；`null`／空白／空字串 code 必須丟 `ArgumentException` — `CLAUDE.md` §4「never \`new Failure()\`」的配套單元測試 | `backend/tests/SharedKernel.Tests/Domain/FailureProviderTests.cs`（1 個 `[Fact]` + 1 個 `[Theory]`×5 = 6 個測試案例） | push 前 / CI | 腳本 |
| **`scripts/coverage-check.sh`（`docs/adr/adr-014-handler-coverage-gate.md`）** ||||
| 19a | concrete `*Handler` 類別 coverage ≥ 80%（逐類判定，async state machine 併回母類）— `CLAUDE.md` §4「coverage ≥ 80% per Handler class」+ `docs/adr/adr-014-handler-coverage-gate.md` | `scripts/coverage-check.sh`（`scripts/ci-checks.sh` full 呼叫，測試段附掛 `--collect:"XPlat Code Coverage"`） | push 前 / CI（僅 full） | 腳本 |
| **NuGet audit（`backend/Directory.Build.props` `WarningsAsErrors`）** ||||
| 19b | NuGet 套件（含 transitive）High/Critical 弱點警告（NU1903/NU1904）升為 build error，不再只靠人／AI 盯 warning 輸出 — `CLAUDE.md` §Core Principles「Security First」+ `docs/adr/adr-015-dependency-vulnerability-audit-gate.md` | `backend/Directory.Build.props`（`WarningsAsErrors` 含 `NU1903;NU1904`） | 每次 restore/build（fast 的 format 段、full 的 build 段、CI 共用同一 build） | 腳本 |
| **Roslyn analyzer gate（`docs/adr/adr-016-roslyn-analyzer-gate.md`）** ||||
| 19c | 語意層品質規則（`CancellationToken` 傳播經 CA2016 直接命中、`throw ex;` 經 CA2200 直接命中、文化敏感字串操作經 CA1304/CA1310/CA1311 直接命中）+ FluentAssertions 強制（禁 `xunit.Assert.*`）— 協調憲章明文規則 (i) + `docs/adr/adr-016-roslyn-analyzer-gate.md` | `backend/Directory.Build.props`（`AnalysisLevel=latest-recommended` + `CodeAnalysisTreatWarningsAsErrors=true`）+ `Microsoft.CodeAnalysis.BannedApiAnalyzers`（測試三專案 `PackageReference` + 共用 `backend/tests/BannedSymbols.txt`） | 每次 restore/build（fast 的 format 段、full 的 build 段、CI 共用同一 build） | 腳本 |
| **HMAC 金鑰雜湊（docs/adr/adr-017-key-hash-hmac-and-hotpath-contract.md）** ||||
| 19d | KeyHash = Base64(HMACSHA256(pepper, rawKey)) 確定性／pepper 敏感性／輸出形狀；pepper 缺值或 < 32 bytes 啟動 fail-fast — docs/adr/adr-017-key-hash-hmac-and-hotpath-contract.md Implementation Rules 3/5 | backend/tests/FunctionalTests/Infrastructure/HmacApiKeyHasherTests.cs | push 前 / CI | 腳本 |
| **學習迴圈 triage（`docs/adr/adr-018-failure-triage-and-observations-retirement.md`）** ||||
| 19e | phase 收尾更新 `tasks/checkpoint.md` 前必跑 failure triage；`REPEAT` 簽名三選一處置（lesson / todo / checkpoint 記不轉理由）— `docs/adr/adr-018-failure-triage-and-observations-retirement.md` 決策 §3 | `scripts/failure-triage.sh`（報表）＋人工判讀 | phase 收尾 | 人／大型模型 |
| **AI review 類** ||||
| 20 | Code review：bug 偵測、安全性稽核、依賴影響分析 | `.claude/skills/code-review/SKILL.md`（PR mode / Self mode） | review 時 | 中型模型 |
| 21 | Orchestrator review executor 產出：事實覆核（不接受概括摘要）、誠實申報覆核 — `docs/orchestration.md` §2 Executor Contract 第 3 條「誠實申報 blocker」的覆核方；第 5 條 unverified_success 條款（`docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (a)）明文化「協調者親自執行確定性檢查才能升級為已驗證」 | 無獨立腳本檔——純人工/大型模型執行的 review 步驟，權威來源見 `docs/orchestration.md` §2 與 `tasks/lessons.md` 對應條目（簡體字掃描已由第 16a 項機械化，不再屬 review 責任） | review 時 | 大型模型 |
| **人工類** ||||
| 22 | ADR PR review checklist（7 項 judgment 檢查：Context 並排引用 / Decision 邊界 / code 範例 / Rationale 三問 / ≥3 Alternatives / Implementation Rules 可打勾 / 同步項目同 commit） — `docs/adr/_template.md` Review Checklist 註解區（ADR-013 決策 (d) 由 CLAUDE.md 遷移） | 無腳本；本體見 `docs/adr/_template.md` Review Checklist 註解區（ADR-013 決策 (d) 遷移） | review 時 | 人 |
| **`.claude/hooks/pre-tool-bash.py` — Bash 指令寫時攔截，2 個 pattern（僅 Claude Code harness 有效）** ||||
| 23 | heredoc（`<<`／`<<-`，排除 herestring `<<<`）攔截——heredoc 寫檔曾致本 harness 背景卡死 3.5 小時，見 `tasks/lessons.md` heredoc 條 | `.claude/hooks/pre-tool-bash.py`（`_HEREDOC` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本（hook，exit 2 阻擋） |
| 23a | zsh 對裸 `=` 開頭參數（`=word` 展開）攔截——對應 `(eval):N: == not found` 事故，見 `tasks/lessons.md` zsh 等號條 | `.claude/hooks/pre-tool-bash.py`（`_ZSH_EQUALS_TOKEN` regex 段） | 寫的當下（**限 Claude Code harness**） | 腳本 |

不列 Tessl（`tasks/process-improvement-plan.md` §9.3 D-2 裁決：擱置，不入制度）。

---

## 無防線區塊（規則存在但查無機械化檢驗，⚠️）

以下規則在 `CLAUDE.md` / ADR 中明文存在，但實查 `scripts/`、`.claude/hooks/`、`backend/tests/Architecture.Tests/` 後找不到對應機械化檢驗；照實標注，不假裝有防線。

| 規則 | 權威來源 | 追蹤狀態 |
|---|---|---|
| ~~Unit test coverage ≥ 80%（Handler code）~~ | ~~`CLAUDE.md` §4 Verification Standards 條列「coverage ≥ 80%」~~ | ✅ **2026-07-05 已機械化** — 規則落點 `docs/adr/adr-014-handler-coverage-gate.md`，防線見主表第 19a 項（`scripts/coverage-check.sh`，`scripts/ci-checks.sh` full 呼叫）；自本區塊移出 |
| 每個 Guard condition 須同時有正向與負向情境 | `CLAUDE.md` §4 Verification Standards 條列「each Guard has positive AND negative scenarios」 | 未追蹤——**2026-07-05 使用者裁決不機械化**（`.feature` 正負配對比對腳本成本高於效益；由 BDD 撰寫紀律 + AI review（主表第 20 項）承擔），比照 `.Value` 條先例 |
| `NEVER` 存取 `.Value` 前未先檢查 `.IsFailure` | `CLAUDE.md` §4 Verification Standards 條列「Error handling / code quality」 | 未追蹤——需資料流/Roslyn analyzer 才可靜態偵測，`backend/tests/Architecture.Tests/` 未見對應測試；**ADR-016 §4 裁決不機械化**——自寫 dataflow analyzer 成本高於現階段效益，由 AI review（第 20 項）+ BDD 行為驗證承擔 |
| `NEVER` 使用空 catch block | `CLAUDE.md` §4 Verification Standards 條列「Error handling / code quality」 | 未追蹤——內建 CA 無對應規則；ADR-016 §4 裁決不為單一規則引入 Sonar 全家桶，維持無防線 |
| ~~`NEVER` 用 `throw ex;`（須 `throw;`）~~ | ~~`CLAUDE.md` §4 Verification Standards 條列「Error handling / code quality」~~ | ✅ **2026-07-05 已機械化** — 經 `docs/adr/adr-016-roslyn-analyzer-gate.md` 由 CA2200 直接命中（防線見主表第 19c 項）；自本區塊移出 |
| ~~`CancellationToken cancel` 須傳播到每個 I/O 呼叫（EF Core / HTTP client / message bus）~~ | ~~`CLAUDE.md` §4 條列「\`CancellationToken cancel\` propagated to every I/O call」~~ | ✅ **2026-07-05 已機械化** — 經 `docs/adr/adr-016-roslyn-analyzer-gate.md` 由 CA2016 直接命中（防線見主表第 19c 項），現況零命中屬防患型（違規第一次出現時即攔）；自本區塊移出 |
| ~~命名慣例：一般 PascalCase 方法 / `_camelCase` 欄位 / `Async` 後綴~~ | ~~`.claude/references/dotnet/naming.guide.md` §B（原 `CLAUDE.md` §4 命名條文已由 ADR-013 瘦身移除，現行 §4 無對應措辭）~~ | ✅ **2026-07-04 已機械化** — 規則落點 `docs/adr/adr-011-naming-rules-editorconfig-enforcement.md`，防線見主表第 11 項（`backend/.editorconfig` `dotnet_naming_*` + `EnforceCodeStyleInBuild`，`dotnet build` 與 `dotnet format --verify-no-changes` 皆會擋下）；`backend/tests` 排除 Async 後綴為 ADR-011 §3 明文 carve-out（BDD step 語意衝突），非機械化缺口；自本區塊移出 |
| ~~FluentAssertions 用於測試斷言，禁止直接比較（如 `Assert.Equal`）~~ | ~~`CLAUDE.md` §4 條列「FluentAssertions in tests」~~ | ✅ **2026-07-05 已機械化** — 經 `docs/adr/adr-016-roslyn-analyzer-gate.md` `Microsoft.CodeAnalysis.BannedApiAnalyzers` 禁 `T:Xunit.Assert`（防線見主表第 19c 項）；自本區塊移出 |
| API Key validation latency P99 < 50ms | `CLAUDE.md` §4「Performance (hotpath changes only)」 | 未追蹤——repo 內無負載測試腳本或效能基準測試；ADR-017 Implementation Rule 6 已排定 — validation slice DoD 含 perf smoke 並同 commit 登記入本表 |
| Validation throughput ≥ 100 RPS | `CLAUDE.md` §4「Performance (hotpath changes only)」 | 未追蹤——同上，無對應腳本；ADR-017 Implementation Rule 6 已排定 — validation slice DoD 含 perf smoke 並同 commit 登記入本表 |
| ~~禁止簡體字（正體中文文件）~~ | ~~全域層級規則，repo 內無明文、無 lint~~ | ✅ **2026-07-04 已機械化** — 規則落點 `docs/adr/adr-009-traditional-chinese-and-zh-lint.md`，防線見主表第 16a 項；自本區塊移出 |
| Refactor 紀律：production-only / test-only 不混改（介面/DTO 改名與 wire-contract 變更豁免） | `CLAUDE.md` §BDD「Refactor discipline」段 | 未追蹤——**2026-07-05 使用者裁決不機械化**（合法例外多，staged 路徑比對必然誤報；由 review 承擔） |
| `tasks/bdd-backlog.md` → `tasks/bdd-progress.md` 晉升只能由使用者執行 | `CLAUDE.md` §BDD Kanban 段 | 本質不可機械化（無法從 diff 判定「誰」決定的）；由 review 與 git 歷史事後稽核承擔，明文承認無防線 |
| 驗證矩陣同步義務（機制異動同 commit 登記本表） | 本檔治理聲明「同步義務」段 | 半防線——`scripts/machinery-check.sh` 驗「本表引用的路徑存在」，不驗反向「新機制已登記」；缺口明文承認，靠 ADR 同步項目清單與 review 承擔 |

---

## 審校紀錄（2026-07-04 orchestrator review）

- Executor 自查時回報的 4 項並行時序落差（hook-smoke 未接線、`pending-lessons.jsonl` 未刪、`.gitignore` 缺 `.claude/*.marker`、第 16 項為預寫狀態），於本表 commit 時**已全部消解**：Phase B 落地 + orchestrator 裁決 `hook_smoke` 同時接入 fast 與 full（維持「fast ⊂ full」不變式）。
- **仍開放**：`dotnet format` 的權威來源模糊（主表第 11 項）——`backend/.editorconfig` 存在，僅含 2 檔 `generated_code` whitespace 豁免，style/naming 規則未定義，格式規則對應不到任何 `CLAUDE.md`/ADR 條文，權威來源仍為工具預設。裁決狀態維持待規格擁有者決定，追蹤於 `tasks/process-improvement-plan.md` §8.3。
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
OK   scripts/git-hooks/commit-msg
OK   .github/workflows/ci.yml
OK   scripts/hook-smoke.sh   (已接入 ci-checks.sh fast + full——見審校紀錄)
```
