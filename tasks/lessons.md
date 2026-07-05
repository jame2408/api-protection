# Lessons Learned

Patterns and lessons captured during development. Updated automatically per Self-Improvement Loop rules in CLAUDE.md.

> 分區治理：`## Active` 與 `## Archived` 兩區受 `docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (b) 管轄。歸檔判準：落地已成為機械化 gate（測試 / lint / hook）者歸檔；`.claude/hooks/session-init.sh` 只注入 `## Active` 區每條的標題 + `**Rule:**` 行。修改分區判準須先開新 ADR。

## Trigger conditions
- User correction or pushback
- Self-correction after failed command or wrong approach
- Non-obvious technical decision (architecture, library choice, tradeoff)
- Non-trivial or surprising bug root cause
- Repeated issue (second occurrence)
- User confirms a non-obvious approach worked

---

<!-- Entries added below as they occur. New entries go under ## Active. -->

## Active

> 尚未有機械化防線接管的教訓；`session-init.sh` 每個 session 注入以下每條的標題與 Rule 行。

### [correction] Orchestrator 越位執行細節 — 路由表也約束 orchestrator 自己
**Date:** 2026-07-04
**Context:** 使用者糾正：zh-lint 實作、檔案修正、commit 操作等細節工作由 orchestrator（大型模型）親自執行，違反 docs/orchestration.md §1 自己訂的路由表（實作屬中型模型）。「規劃者不下場」不只是成本原則，也是憲章可移轉性的驗證 — orchestrator 自己繞過路由表，等於憲章沒有被完整遵守。
**Rule:** orchestrator 只做：設計裁決、ADR 起草或規格撰寫、review、與使用者的決策互動。任何有明確規格可循的實作（腳本、文件編輯、git 操作、勘誤）一律派 executor，即使「自己做比較快」。界線澄清：checkpoint 產出、以及親自驗證後的放行 commit/push，屬 orchestrator 的交接與 gate 職責，不算越位。豁免（2026-07-05 使用者裁決）：「內容已完全確定的機械性勘誤」— 改動的逐字內容在 orchestrator 既有 context 中已完全定案、零判斷成分、且派工固定成本明顯超過任務本身（如 2–3 行登記簿補記）— 得由 orchestrator 直接執行；任何需要 executor 重新閱讀檔案「產生內容」的編輯不在豁免內。
**落地:** 本條 lesson + Phase E 起全部實作改派 executor（本任務即範例）；界線澄清與機械性勘誤豁免皆為 2026-07-05 使用者裁決。

### [correction] Token 經濟四個反模式：巨型任務包、resume 大 transcript、馬拉松 session、limit 中斷後原地續舊 session
**Date:** 2026-07-05
**Context:** 使用者發現單一句「先繼續」使 5h 用量瞬間 +37%。root cause 三層：(1) Phase I 規格把四階段捆成一包，養出 225K tokens / 111 tool calls 的巨型 executor；(2) orchestrator 用 SendMessage resume 該 agent 續行 — resume 會把整份巨型 transcript 無快取重讀計費，正確做法是開新 executor + 小規格（checkpoint 就是為此存在）；(3) orchestrator 自己的 session 從盤點跑到 Phase I 不曾重啟，每次使用者發話都重讀全史 — 對 executor 執行了「任務切小」卻沒對自己執行。同日 [repeat]：limit 中斷恢復後在原 session 說「繼續」+ resume 死掉的 executor → 瞬間 +13%（prompt cache TTL 5 分鐘，limit 空窗必然全冷，恢復第一輪把整段對話史與 executor transcript 以未快取輸入重讀）。
**Rule:** (1) executor 任務規格以單一階段為原則，預估超過 ~50 次工具呼叫就拆包；(2) resume 既有 agent 只限小型追問；需要續行長任務時一律新開 executor、以 spec/checkpoint 銜接；(3) orchestrator session 以一個 Phase 為壽命上限，Phase 落地即結束 session，下個 Phase 冷啟動接 checkpoint；(4) limit／服務中斷視同 phase 邊界 — 快到 limit 時最後一動是 checkpoint 落盤，恢復一律開新 session 接 checkpoint（僅當舊對話有未落盤狀態才回去，先落盤再換）；中斷的 executor 一律新開接手（讀 spec + 檢視現有 `git diff` 從斷點續跑），不得 resume。
**落地:** 本條 lesson；納入下一個憲章修訂（ADR-013 候選：token 經濟條款從原則升為可打勾規則）— 在此之前由 §8.5 checkpoint 的「如何接上」段執行 session 重啟紀律。

### [correction] 自動載入面有 token 預算 — 不放日期出處、不複寫憲章、先查既有落點
**Date:** 2026-07-05
**Context:** 使用者連環糾正 CLAUDE.md 新增的 Orchestrator Brief：加了無操作意義的日期、五條內容有四條複寫 orchestration.md（違反 ADR-007 規則 5 / SSOT）、CLAUDE.md 因此變胖 — 且這是反射式補丁，動手前沒全盤檢查內容是否已有權威落點。
**Rule:** 動自動載入面（CLAUDE.md / session-init 注入 / AGENTS.md）前先問三題：(1) 這內容已有權威落點嗎？有 → 只放指針；(2) 每一行對「下個 session 的行為」有操作意義嗎？沒有（日期、出處、敘事）→ 刪，出處查 git；(3) 改完後自動載入總量是變大還是持平？CLAUDE.md 的正確內容 = 高層 workflow + non-negotiable + 指派與指針（§2 根因 1 處理原則早已寫明）。
**落地:** Brief 12 行縮至 3 行（本 commit）；本條 lesson。

### [correction] 啟用型 BDD 場景直接綠 — 測試「會失敗」的能力未被證明，必須補故意紅
**Date:** 2026-07-05
**Context:** scenario「租戶狀態非 Active — 拒絕建立」的 slice 早已完整（guard／HTTP 映射／steps 全就位），移除 `@ignore` 後直接綠，整個週期沒有紅過 — vacuous pass 風險未被排除。使用者稽核後裁定補為義務。
**Rule:** 凡「移除 `@ignore` 後未經 Red 直接 Green」的啟用型場景，必須做一次故意紅：暫時破壞對應 guard（或斷言期望值）→ 跑測試取得紅的原始輸出 → 還原 → 確認回綠；紅與綠兩份輸出都列入回報。此事實由指令輸出證明，不由 AI 宣稱「測試有意義」代替。
**落地:** `tasks/_templates/executor-spec.md`「故意紅」欄（本 commit）。

### [correction] Executor 派工規格必須內建取證指令與 friction 欄位 — 回報品質是 spec 精度問題
**Date:** 2026-07-05
**Context:** executor 為滿足「scenario 名稱 + Passed 原文」的回報要求，自行摸索跑了 3 次 test suite（其中一次 `grep "Failed"` 誤中 MSBuild 雜訊行而整次無效）；另有 4 條 blocker 以下的不順（繞路、重跑）靠 orchestrator 事後追問才浮現。
**Rule:** 派工一律用 `tasks/_templates/executor-spec.md`：(1) 驗證與取證步驟由 spec 給死可執行指令與預期取證行（例如 `dotnet test … --logger "console;verbosity=detailed"`），能機械化的驗證不留給 executor 判斷「怎麼證明」；(2) 回報格式必含「非 blocker 的不順與繞路」欄位，friction 常態收集，不靠追問。
**落地:** `tasks/_templates/executor-spec.md`（本 commit）。

### [decision] 制度凍結啟發式 — 治理機制視為 feature-complete，新機制只能事故驅動
**Date:** 2026-07-05
**Context:** 使用者問「ADR 強制機制是否有過度制度化風險」。盤點結論：15 個 ADR 幾乎全為事故驅動（良性形態），但儀式成本單一級距（兩行 props 改動配 150 行 ADR）、Proposed 狀態實際未使用（皆當日 Accepted）、制度能量與產品能量失衡（產品級 ADR 僅 3 個，hash 決策懸置，BDD 4/44）。根本動力學：AI 使制度生產極便宜，歷史上限制官僚化的稀缺性消失，自然漂移方向是「更多制度」。
**Rule:** (1) 治理機制視為 feature-complete——新防線／新模板／新流程只在「觀察到的失敗」後追加，禁止投機性立法；解決制度問題優先用裁決習慣，不新增制度形態（含「輕量化 ADR 模板」這類二階制度）。(2) 制度預算讓位產品側——下一份 ADR 應是 hash 演算法（產品架構級），其後重心為 BDD 進度，直到再有事故證明治理有洞。
**落地:** 本條 lesson（使用者 2026-07-05 裁決採納兩條啟發式）。

### [info] CodeAnalysisTreatWarningsAsErrors 只升級 CA 前綴 — 其他 analyzer 診斷需顯式 severity=error
**Date:** 2026-07-05
**Context:** ADR-016 故意紅驗證時發現：`Xunit.Assert` 的 banned-symbol 違規只出 `warning RS0030`、不擋 build——`CodeAnalysisTreatWarningsAsErrors` 語意上僅提升 `CA*` 前綴診斷，RS／xUnit／第三方 analyzer ID 不在覆蓋範圍。沒做故意紅就會上線一個不會咬人的 gate。
**Rule:** 引入非 CA 前綴的 analyzer 規則時，阻斷性必須以 `.editorconfig` 的 `dotnet_diagnostic.<ID>.severity = error` 顯式設定，且一律用故意紅證明真的會使 build 失敗；不得假設 TreatWarningsAsErrors 類屬性已涵蓋。
**落地:** `backend/tests/.editorconfig` RS0030 段（commit `7bb4053`）；本條 lesson。

### [correction] 「不存在」的斷言也要機械化驗證 — 矩陣誤報 .editorconfig 不存在
**Date:** 2026-07-04
**Context:** 驗證矩陣與 plan 宣稱「repo 無 .editorconfig」，實際 backend/.editorconfig 存在（executor 只查 repo root，orchestrator 抽驗也未抓到）。「存在性」核對清單只驗證了「列出的檔案存在」，沒驗證「宣稱不存在的東西真的不存在」。
**Rule:** 寫「X 不存在」的結論前，必須用遞迴搜尋驗證（如 find . -name 'X' 或 git ls-files '**/X'），不能只看單一目錄。
**落地:** 矩陣與 plan 勘誤（本commit）；本條 lesson。

### [correction] Spec 背景欄的執行期值敘述必須讀宣告求證 — null 推測害 executor 白跑一輪
**Date:** 2026-07-05
**Context:** 派工「Active 金鑰數達到上限」場景時，spec 背景欄寫「`_ctx.CurrentTenantId` 此場景中為 null」— 這是「沒有 Given 設過它」的推測，未讀 `FunctionalTestContext` 宣告（實為 `= string.Empty` 預設）。executor 依 spec 寫 `is null` 條件恆假，多跑一輪測試才自行改成 `string.IsNullOrEmpty` 修正。
**Rule:** spec「背景（已核實事實）」欄凡涉及執行期值（預設值、null 與否、初始狀態），必須讀該欄位/屬性的宣告與初始化行求證，不得從「沒人設定過」推論為 null；核實深度以「executor 可直接照抄判斷式」為準。
**落地:** 本條 lesson（spec 不入 repo，無檔案可勘誤）。

### [correction] ADR 改寫被引用文字時，同步項目須 grep 反查「逐字引用者」
**Date:** 2026-07-05
**Context:** 全 repo 衝突掃描發現 `docs/verification-matrix.md` 有 5+ 處逐字引用 ADR-013 瘦身前的舊 CLAUDE.md §4 文字（grep 全數 0 命中），另 checklist 本體位置說法錯誤 — 根因是 ADR-013 改寫 CLAUDE.md 時，「同步項目」清單只列了被改的檔案，沒列「逐字引用被改文字的文件」。同型還有：新 lint 上線（source-lint CreateScope 段）未反查 rule 檔既有範例是否會被攔（di.rule.md §D ✅ 範例直接違規）。
**Rule:** 任何修改「會被其他文件逐字引用的文字」（CLAUDE.md 條文、ADR 決策句、lint 豁免範圍）時，同步項目清單必須先 grep 反查引用者（含 `docs/verification-matrix.md` 與 `.claude/references/`）並列入同 commit；新 lint 上線前反查 rule 檔的 ✅ 範例是否落在禁令內。
**落地:** 本次修繕 commit `be0152e`；本條 lesson。

## Archived（已機械化 — 防線代記）

> 落地已成為機械化測試 / lint / hook，防線本身即代替記憶；保留於此僅供追溯，不再注入 session context。

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
**Rule:** 機械化一條規則前先問「違規長在哪個層次」：型別依賴→NetArchTest；型別/成員 metadata→reflection；method body/語法→grep（`scripts/source-lint.sh`）或 Roslyn analyzer。grep 的好處是 cheap，可放進 pre-commit fast 模式即時擋。命名豁免（如 `new Failure(` 的 `FailureProvider.cs`）必須在 lint 內明文排除，不是默契。
**落地:** `LoggerBoundaryTests.cs` / `NamingConventionTests.cs`（reflection）+ `scripts/source-lint.sh`（grep，接進 `ci-checks.sh` fast+full）。Architecture.Tests 3→11 tests。

### [decision] 寫的當下 PreToolUse hook 只攔「高信心、與下游一致」的 pattern，不攔 throw
**Date:** 2026-06-13
**Context:** 補最內層防線（編輯當下攔截）時，plan §B2 原列要攔 `new Failure(`/`throw`/`ILogger`/`ct` 四類。實查 `throw new` 在 src 的分布：`Result.cs` 存取器守衛、`InfrastructureModule.cs` 設定守衛、`IConsumerValidator` 參數驗證——全是合法 throw。文字層級攔 `throw` 會大量誤報，而誤報的 hook 比沒有更糟（訓練人/agent 忽略或關掉它）。
**Rule:** 寫的當下 hook 只攔「文字層級可零誤報、且已在 source-lint/架構測試強制」的 pattern（`new Failure(` 豁免 FailureProvider、bare-string code、`cancel` 命名、`ILogger<` 於 Service/Domain/Handler 路徑）。需要語意判斷或會誤報的（如 `throw`）留給 reflection 架構測試的結構性檢查，不放進文字 hook。hook 與 source-lint 共用同一組 pattern → 四層防線（寫/commit/push/CI）規則一致不漂移。
**落地:** `.claude/hooks/pre-tool-edit.py` + `.claude/settings.json` PreToolUse 註冊（matcher `Edit|Write|MultiEdit`）；exit 2 擋並回報。9 情境測試全對。

### [decision] 改 wire contract 必須同 commit 更新斷言它的測試（否則套件紅）
**Date:** 2026-06-24
**Context:** 把 error 從 `{error}` 改成 RFC 9457 時，發現 `CreateApiKeySteps.ThenCreateFailsWithReason` 原本把 body 反序列化成 `record ErrorResponse(string Error)` 斷言 `body.Error`。若只改 production 不改測試，既有通過場景立刻紅。
**Rule:** 變更 API wire contract（error 形狀、回應欄位）時，斷言該契約的測試必須同一個 commit 一起改 — 這是「契約變更」不是「test refactor」，不違反「production/test 不混改」（那條針對純 refactor）。順手把斷言升級成鎖完整 RFC 9457 shape，一改鎖住所有用該 step 的場景（含 @ignore 未上線的）。
**落地:** `CreateApiKeySteps.cs` `ThenCreateFailsWithReason` 改斷言 RFC 9457（type/title/status/errorCode/traceId + content-type）；故意紅驗證通過。

### [correction] Executor 產出含簡體字 — 「禁簡體」規則存在但無機械化防線
**Date:** 2026-07-04
**Context:** Phase A executor（Sonnet 級）在 adr-007 Rationale 寫出「执行」。禁用簡體是全域層級規則，但 repo 內無明文、無 lint，任何 executor（尤其非 Claude harness）都可能重犯；本次靠 orchestrator review 的簡體字元掃描才攔下。<!-- zh-lint:allow：本行刻意引用違規字元 -->
**Rule:** Review executor 產出的中文文件時，必須跑一次簡體字元掃描；接受 executor 報告「驗證全綠」不等於內容合規 — 報告只覆蓋它被要求跑的檢查。
**落地:** adr-007 修正（commit `d8a006b`）→ 2026-07-04 同日完成機械化：`docs/adr/adr-009-traditional-chinese-and-zh-lint.md` + `scripts/zh-lint.sh`（OpenCC 字表，接入 ci-checks fast+full）。過程中手寫掃描清單兩度漏字、orchestrator 本人 commit message 也違規一次 — 證明此類字元級規則必須用完整字表機械化，人工檢出不可靠。

### [decision] HTTP boundary helper 放 BC 內，不放 Host（BC→Host 會循環引用）
**Date:** 2026-06-24
**Context:** Phase 3 對齊 RFC 9457 時，原計畫把 `ApiProblem` error-mapping helper 放 `Host/Http/`。但 endpoint（`CreateApiKeyEndpoint`）住在 KeyLifecycle BC，BC 呼叫 Host 會造成循環引用（Host 已 reference 各 BC）→ 編譯不過。SharedKernel 又是純 domain、不該注入 ASP.NET 型別。
**Rule:** HTTP boundary helper（回 `IResult`、用 `HttpContext`）放在「擁有該 endpoint 的 BC」內（如 `KeyLifecycle/Http/`，該 BC 已有 `FrameworkReference Microsoft.AspNetCore.App`）。等第二個 BC 也需要時再抽共用 web library，別預先放 Host 或污染 SharedKernel。dependency 方向：Host → BC → SharedKernel，不可逆。
**落地:** 防線＝編譯器（Host 已 reference 各 BC，BC→Host 必循環引用、build 失敗，違規當下即編譯不過，非事後檢驗）；placement 先例已成程式碼事實 `backend/src/KeyLifecycle/Http/ApiProblem.cs`（單一 error envelope 來源）。

### [info] Directory.Packages.props 內 XML 註解含 `--` 會被 MSBuild 靜默跳過（NU1015）
**Date:** 2026-07-04
**Context:** 冷啟動 executor 落地 todo #36 時，props 檔首次寫入後 `dotnet restore` 全面 NU1015（找不到版本）。root cause 出乎意料：XML 註解內寫了 `--force`，而 XML 註解不得出現 `--`，整份檔案被判定 invalid 後**靜默跳過匯入**，不是 fail-fast 報錯。`-v:diag` 才看得到 "file being invalid"。
**Rule:** MSBuild props/targets 檔的 XML 註解內禁用雙連字號（含 CLI flag 範例如 `--force`）；遇到「集中設定像不存在一樣」的症狀，先用 `xml.dom.minidom.parse` 驗證檔案合法性再查其他方向。
**落地:** 防線＝`scripts/source-lint.sh` MSBuild XML 合法性段（本 commit，對 `git ls-files '*.props' '*.targets'` 逐檔跑 `xml.dom.minidom.parse`）；`backend/Directory.Packages.props` 註解已改寫為無 `--` 版本（commit `1dc717b`）。

### [correction] 寫 production code 前必須主動載入 .claude/references 規則檔
**Date:** 2026-04-03
**Context:** Wave 1 初始實作時，CreateApiKeyHandler 用 throw 做業務邏輯、CancellationToken 命名 ct、ConsumerValidatorService 在 Scoped 服務內建多餘子 scope，三個問題都是因為沒有載入 .claude/references/dotnet/*.rule.md 就直接寫程式造成的。事後 code review 才全部補救。
**Rule:** 每次對這個 project 寫 production code（Handler、Service、Repository、Endpoint）前，必須先讀取 .claude/references/general/*.rule.md 和 .claude/references/dotnet/*.rule.md，確認再動手。核心規則：(1) Service 層用 Result<T,Failure> + FailureProvider.CreateFailure()，不 throw；(2) CancellationToken 參數一律命名 cancel；(3) Scoped 服務直接注入依賴，不用 IServiceScopeFactory.CreateScope()。
**落地:** 三條核心規則全數機械化，不再依賴「動手前讀規則」的人工紀律本身：(1) Result → `backend/tests/Architecture.Tests/HandlerResultReturnTests.cs`；(2) `cancel` 命名 → `scripts/source-lint.sh`（`bad_cancel` 段）+ `.claude/hooks/pre-tool-edit.py`（寫的當下）+ Roslyn CA2016（`docs/adr/adr-016-roslyn-analyzer-gate.md`）；(3) `IServiceScopeFactory.CreateScope()` 禁令 → `scripts/source-lint.sh` CreateScope 段（本 commit，`*Middleware.cs`/`Program.cs` 豁免）。「動手前讀規則」的提醒本身仍由 `.claude/hooks/session-init.sh` 每個 session 注入。

### [info] 本機 bash 是 macOS 內建 3.2 — 腳本禁用 mapfile；set -e 下 RETURN trap 不觸發
**Date:** 2026-07-05
**Context:** coverage gate 落地時 executor 兩度踩雷：(1) `mapfile -t`（bash 4+ 內建）在 `/bin/bash` 3.2.57 直接 `command not found`；(2) 想用 `trap ... RETURN` 清暫存目錄，實測 `set -euo pipefail` 下函式因錯誤中止時 RETURN trap 不會觸發，清理靜默失效。
**Rule:** repo 腳本以 bash 3.2 為相容底線：清單蒐集用 `while IFS= read -r -d '' … done < <(find … -print0)`，不用 `mapfile`；暫存清理不依賴 RETURN trap，改「執行前 `rm -rf` + `mkdir -p` 固定路徑」模式。
**落地:** `scripts/coverage-check.sh`、`scripts/ci-checks.sh` coverage 段（commit `e94a381`）；防線再加一層 `scripts/source-lint.sh` bash 相容段（本 commit，掃 `scripts/*.sh` 與 `.claude/hooks/*.sh` 禁 `mapfile`/`readarray`/`trap ... RETURN`，自身排除）。
