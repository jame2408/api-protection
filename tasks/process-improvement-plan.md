# 開發流程與制度補強 Plan

> 本 plan 記錄 hardening pass 後的閉環工程（loop engineering）補強方向：哪些 drift 根因已被關閉、哪些仍是開環、以及後續應以什麼順序建立機械化防線。
> 初稿：2026-05-01。更新：2026-06-13。狀態：執行計畫草稿，下一步應拆出正式 ADR 與具體 backlog。

---

## 1. 目前已完成的基線

這次 hardening 之後，以下工作已落地，後續計畫不應再把它們列為待辦。

### 已接受的 ADR

- **ADR-003**：釐清 Repository / Handler / HTTP boundary / cross-BC contract 的錯誤處理責任。
  - Repository 回 raw type。
  - BC internal Handler / Service 回 `Result<T, Failure>`。
  - `SharedKernel/Contracts` 可使用 contract-specific DTO carve-out。
- **ADR-004**：定案 `Failure` 為 `record Failure(string Code)`。
  - `CLAUDE.md` 的 Error Handling 規則已改為「診斷 context 由 boundary logger 補上」。
  - `FailureProvider.CreateFailure` 已加入 null / whitespace guard。
  - `SharedKernel.Tests` 已覆蓋 `FailureProvider` 行為。
- **ADR-005**：Primary Constructor 作為 production DI 預設。
  - `di.rule.md`、`naming.guide.md`、`testing.guide.md` 已補上 settings snapshot / test fixture / anti-pattern 範例的邊界說明。
  - Endpoint parameter injection 與 production constructor DI 已分清楚。
- **ADR-006**：`ApiKeyStatus` enum 採 PascalCase，wire format 交給 `JsonStringEnumConverter(allowIntegerValues: false)`。
  - DTO 已改用 enum 欄位。
  - Functional test 已以 raw JSON literal 鎖定 `lifecycleStatus: "Active"`。
  - `.feature`、step、design docs、reference docs、skill examples 已同步 PascalCase。

### ADR 格式與治理工具鏈

- `docs/adr/_template.md` 已建立，要求固定章節、stable anchors、Alternatives、Implementation Rules 與 governance clause。
- ADR-001、ADR-002、ADR-003、ADR-004、ADR-006 已 retrofitted 到新格式。
- `scripts/adr-lint.sh` 已建立，檢查：
  - Status 格式。
  - 必要章節。
  - Implementation Rules 最後一條 governance clause。
  - `file:line` 引用。
  - ADR 編號連續與唯一。
  - Alternatives 必須有 `Rejected.`。
  - Negative / Trade-offs 必須有 `Mitigation:`。
- `scripts/git-hooks/pre-commit` 已建立：
  - ADR staged changes 會觸發 lint。
  - ADR deletion 會觸發 lint。
  - 拒絕 tracked ADR partial staging。
  - 拒絕 untracked `docs/adr/adr-*.md` 影響 working-tree glob。
- `scripts/install-git-hooks.sh` 已建立，用 `core.hooksPath = scripts/git-hooks` 安裝 repo-local hooks。

---

## 2. 這次 drift 的根因回顧

這些根因已經有部分被 ADR 與 reference docs 修掉，但仍值得保留作為後續防線設計的依據。

### 根因 1：規範散在多處，缺少單一真相來源

過去同一條規則常被寫在多個地方，且各自演化：

- `CLAUDE.md` 的 Error Handling 段曾要求把 diagnostic context 放進 `Failure` message / metadata，但 production `Failure` 只有 `Code`。
- `CLAUDE.md` 的「Service 必須回 Result」規則沒有寫出 `SharedKernel/Contracts` carve-out。
- `naming.guide.md` 的 `_camelCase` field guidance 沒有區分 production DI、settings snapshot、test fixture field。
- `api-spec.md` 承諾 PascalCase status literal，但 C# enum 曾使用 `ALL_CAPS`。

處理原則：細節規則應只在 ADR / rule docs 有一份 authoritative wording；`CLAUDE.md` 只保留高層 workflow、non-negotiable constraints 與 ADR 指針。

### 根因 2：規範只以文字存在，違規可以靜默存在

沒有機械化檢驗時，以下違規不會讓 build / test 變紅：

- Repository interface 回 `Result<,>`。
- `Failure` 被加 message / metadata 欄位。
- Handler / Service 注入 `ILogger<T>`。
- BC 之間直接 reference。
- API error response drift away from RFC 9457 ProblemDetails。

處理原則：重要規則至少要有一層 fail-fast 檢驗。第一層先做 architecture / functional tests，之後再考慮 hook 或 analyzer。

### 根因 3：教學範例偏離專案真實語法

過去 reference docs 使用虛構符號與舊式樣板，例如 `OrderRepository`、`_userService`、Controller `[HttpGet]`、`this.Failure`。AI agent 會直接複製範例，因此範例 drift 的傷害大於規則文字 drift。

處理原則：範例優先使用本專案真實 patterns；若為 generic example，必須清楚標示它不是 production pattern。

### 根因 4：Agent 啟動時不保證讀到正確規則

目前仍依賴人提醒或 skill 自律載入 references。這是開環：規則存在，但沒有強制 signal 確保它被讀到。

處理原則：先強化 skill 的 must-read 行為；hook block 等更硬的機制留到後面，避免過早建立 brittle guardrail。

### 根因 5：API contract 缺少足夠 contract tests

Status enum wire format 已由 ADR-006 補強，但 RFC 9457 ProblemDetails、`truncatedKey` 等 API spec drift 仍需要 functional tests 鎖住。

處理原則：public API contract 不只測 status code，還要測 response body shape 與 wire literal。

---

## 3. 目前仍開環的事項

> 狀態更新（2026-06-13）：以下多數項目已關閉，逐項落地狀態見 §8.2；本節保留為當時的開環描述。

### 開環 A：Architecture.Tests 仍未成為真正防線

`Architecture.Tests` 仍需 seed 最小可用測試，否則 ADR-003 / ADR-004 / ADR-005 的核心規則仍只靠文件與 review。

最小防線：

1. BC isolation：BC 之間不能直接 reference；只允許透過 `SharedKernel/Contracts`。
2. Repository raw return：Repository interface 不回 `Result<,>`。
3. Boundary logging：Domain / Handler / Service 不注入 `ILogger<T>`。
4. Failure shape lock：`Failure` 只能有 `Code`。

### 開環 B：API contract tests 還不完整

需要補：

1. RFC 9457 ProblemDetails golden tests。
2. `CreateApiKeyResponse` 的完整 wire contract，例如 `truncatedKey`。
3. Failure code → endpoint response mapping 的測試。

### 開環 C：規範演化流程還沒有正式 ADR

目前這份 plan 已經在提議 governance：

- ADR 是 rule docs 改動的唯一通道。
- 同 commit 同步 ADR / references / tests / hooks。
- lessons 必須落地到 ADR 或檢驗。

這些屬於制度級決策，應拆成正式 ADR（建議下一號 ADR），避免 plan 自己變成另一份隱性規範。

### 開環 D：Agent reference loading 還沒有閉環

`coding-style` 與 `code-review` skill 已有 reference loading 流程，但還不夠強：

- coding-style 仍偏 generic stack detection，未明確要求本專案必讀 ADR-003/004/005/006。
- code-review 會讀 rule files，但需要確保缺少 `nodejs/` / `python/` 等目錄時不把它視為錯誤。
- `session-init.sh` 目前主要 inject lessons，尚未 inject project-specific must-read guidance。

### 開環 E：lessons 還沒有三類模板與落地欄位

`tasks/lessons.md` 應區分：

1. 規範挑戰被接受。
2. 規範挑戰被拒絕。
3. 意外發現。

每一類都需要「落地」欄位，指向 ADR、architecture test、hook、reference doc 或 carve-out。沒有落地欄位的 lesson 只是備忘錄，不是閉環。

---

## 4. 重排後的實作 Phase

> 狀態更新（2026-06-13）：Phase 2 / 5 / 6 已落地、Phase 4 部分落地、Phase 1 / 3 仍待做（見 §8.2）。

後續不要再以「再做一輪 hardening」為目標，而是每個 phase 關閉一個具體開環。每一步都應可獨立 commit，可中斷、可恢復。

### Phase 1：整理制度決策，拆出正式 ADR（0.5 day）

目標：不要讓這份 plan 成為隱性規範來源。

工作：

1. 開新 ADR，暫名：`agent-governance-and-rule-enforcement`。
2. 在 ADR 中決定哪些規則由哪一層防線負責：
   - architecture tests。
   - functional / contract tests。
   - pre-commit hook。
   - agent skill reference loading。
   - future PreToolUse hook（暫不作為第一版）。
3. 在 ADR 中明確列出第一版不做：
   - 不先做 Roslyn analyzer。
   - 不先做 broad PreToolUse block。
   - 不把 `CLAUDE.md` 寫成第二份 rule docs。
4. 把本 plan 的 governance 內容改成指向該 ADR，避免雙寫。

驗收：

- 新 ADR 有 Status / Context / Decision / Rationale / Consequences / Alternatives / Implementation Rules。
- ADR 的 Implementation Rules 列出「修改規則必須同 commit 更新衍生物」。
- `scripts/adr-lint.sh` 通過。

### Phase 2：建立 Architecture.Tests MVP（1 day）

目標：把 ADR-003 / ADR-004 / ADR-005 的核心規則變成紅綠燈。

工作：

1. 補 `Architecture.Tests` 套件與第一批測試。
2. 實作 BC isolation 測試。
3. 實作 Repository raw return 測試。
4. 實作 no `ILogger<T>` in Domain / Handler / Service 測試。
5. 實作 `Failure` shape lock reflection test。
6. 為 `SharedKernel/Contracts` carve-out 寫明豁免，而不是藏在測試實作細節。

驗收：

- `dotnet test backend/tests/Architecture.Tests/` 有實際 test discovered。
- 每條新測試至少實際跑綠一次。
- 若環境允許，為其中至少一條 rule 做一次臨時 red 驗證後 revert。

### Phase 3：補 API contract 防線（1 day）

目標：讓 API spec drift 進 functional tests，而不是等 review 發現。

工作：

1. 定義 error response mapping 的單一 helper / boundary function，避免 endpoint switch 分散。
2. 為 `CreateApiKeyEndpoint` 的主要 failure cases 補 RFC 9457 ProblemDetails raw JSON assertion。
3. 補 `truncatedKey` wire contract 測試與 production 欄位。
4. 補 `VALIDATION_ERROR:` prefix tightening（若採用）與對應測試。

驗收：

- Functional tests 不只檢查 status code，也檢查 `type`、`title`、`status`、`detail`、`errorCode`、`errors` 等 contract 欄位。
- `dotnet test backend/tests/FunctionalTests/` 通過。
- API spec 與 test assertion 的 expected wire literal 一致。

### Phase 4：強化 agent reference loading（0.5–1 day）

目標：先讓 agent 更穩定讀到正確材料，不先做重型攔截。

工作：

1. 更新 `coding-style/SKILL.md`：
   - 本專案 .NET / C# task 必讀 `CLAUDE.md`、core dotnet references、ADR-003/004/005/006。
   - 缺少非本專案 stack 目錄時 skip，不視為錯誤。
2. 更新 `code-review/SKILL.md`：
   - Self Mode / PR Mode 都強制讀 environment refs。
   - Phase 3 rule loading 對不存在的 stack directories 採 skip-if-missing。
3. 擴充 `session-init.sh`：
   - 保留 lessons injection。
   - 第一個 prompt 額外提示 project-level must-read map，但不要塞入大量完整文件內容。

驗收：

- 新 skill instructions 不要求讀不存在的 `nodejs/` 或 `python/` 目錄。
- Review / coding task 的 Phase 0 可明確指出要讀哪些 project-specific references。
- 不新增會誤擋正常工作的 hook。

### Phase 5：建立 lessons 三類模板（0.5 day）

目標：讓學習迴圈有落地欄位，不再只是心得。

工作：

1. 更新 `tasks/lessons.md` 模板或 lesson skill：
   - 挑戰被接受。
   - 挑戰被拒絕。
   - 意外發現。
2. 每個模板都必填：
   - 觸發場景。
   - 決策結果。
   - 落地 ADR / test / hook / reference doc / carve-out。
   - 尚未落地時的 pending item。
3. 將本次 ADR-003/004/005/006 的典型案例各補一條 lesson（若還沒補）。

驗收：

- lessons 不再只有 narrative；每條都能追到落地物或 pending backlog。
- 若 pending 未落地，必須有明確 owner / next action。

### Phase 6：再評估 PreToolUse hook / Roslyn analyzer（optional）

目標：只在前面防線不足時，再增加更硬、更貴的機制。

候選：

1. PreToolUse block：
   - `new Failure(`。
   - Handler / Service / Domain path 內出現 `ILogger<`。
   - 明顯錯誤的 `CancellationToken cancellationToken` / `ct` 命名。
2. Roslyn analyzer：
   - 只有當 grep / architecture tests 無法可靠表達規則時才做。

暫緩理由：

- Hook 會有 runtime / transcript / tool payload brittle 風險。
- Roslyn analyzer 維護成本高。
- 第一版應先使用較穩定的 architecture / functional tests。

---

## 5. 不會做的事

- **不把 hardening pass 排程化**。如果還需要週期性大掃除，表示防線設計仍失敗。
- **不先做 broad PreToolUse block**。先用 tests / hooks / skill loading 建立較穩定的防線。
- **不先做 Roslyn analyzer**。只有當 architecture tests / functional tests / grep 無法表達規則時再評估。
- **不擴充 `Failure` 為 message / metadata**。ADR-004 已 reject。
- **不把 `CLAUDE.md` 寫成完整 rule book**。`CLAUDE.md` 保留高層 workflow、non-negotiable constraints 與 ADR 指針；細節留在 ADR / references。
- **不為 `dotnet format` 加更多 generated-code 豁免**。所有新檔案應通過 `--verify-no-changes`，除非有明確 ADR 或註解理由。
- **不在這個服務內加 Gateway responsibility**。rate limiting、HTTPS termination、CORS 等仍以既有 Gateway 邊界決策為準。

---

## 6. Definition of Success

下次 audit / multi-agent review 不應再產生同規模 drift。成功標準：

- `Architecture.Tests` 至少有上述 4 條 MVP rules，且每次 CI / local test 都會跑。
- 新增 Handler / Repository 時，至少一條 architecture test 會覆蓋它。
- API error response drift 會被 functional tests 抓到，不等 code review。
- 每次規範挑戰都有 ADR 或 explicit rejection lesson。
- 每條 lesson 都能追到已落地檢驗、ADR、hook、reference doc 或 pending backlog。
- ADR lint 與 pre-commit hook 長期維持綠燈。
- `tasks/todo.md` 的 Pending 區塊正常情況下接近空；如果累積超過 5 條 🐞，要優先把共通 root cause 機械化，而不是只逐項清掉。

如果半年後仍需要一次大型 hardening pass，代表這份 plan 的防線設計本身不足，應重新做 loop engineering audit，而不是再補更多文字規則。

---

## 7. 給未來自己的提醒

- 規範修改不是 free；每加一條規則，先問「紅燈在哪裡亮」。
- 範例就是學習材料；範例錯，agent 會照抄。
- 細節只寫一份；多處複寫就是 drift 的起點。
- Prevention 比 cleanup 便宜十倍。
- 同 commit 同步規範與衍生物；分開落地就是主動製造開環。

---

## 8. 2026-06-13 落地紀錄（loop engineering audit → 防線實作）

> 本節併入並取代先前獨立的 `tasks/loop-engineering-audit.md`（單一真相來源，避免兩份 retro 各自演化）。逐表的完整盤點細節留在該檔的 git 歷史。落地工作在分支 `hardening/architecture-tests-mvp`。

### 8.1 四迴圈健康度（audit 快照 → 現況）

| 迴圈 | audit 當時 | 現況 |
|---|---|---|
| 執行 | 半閉（BDD kanban 紀律好，但無 CI 背書） | 半閉（CI 一旦上線即閉） |
| 防線 | **幾乎全開** — 11 條架構規則零檢驗、Architecture.Tests 空殼、無 CI、pre-commit 只跑 adr-lint | **閉** — 11 個架構測試 + source-lint + 四層 gate |
| 規範演化 | 閉（ADR 通道為四環最佳） | 閉 |
| 學習 | 開（lessons 僅 1 條且過時） | 半閉（三類模板在用、新增 3 條落地 lesson；governance ADR 仍待開） |

核心診斷：本專案是典型案例的反面——規則精良、演化通道模範，但守護規則的防線曾是空殼（規則只靠人工 hardening 後的記憶力撐著）。本次把防線從「靠記憶力」轉成「違規自動變紅」。

### 8.2 Delivery Manifest（開環 / Phase → 落地）

| 開環 / Phase | 狀態 | 落地 | commit |
|---|---|---|---|
| §3-A / Phase 2：Architecture.Tests | ✅ | BC 隔離（NetArchTest）+ Repository raw return + Handler 必回 Result + ILogger 邊界 + 命名 + FailureCodes shape + **Failure shape lock（ADR-004 §4，加欄位即 red）**，共 **13 tests**，各綠＋故意紅驗證 | `b86e0f5` `a0d1208` `d427001` |
| 語法層級 lint | ✅ | `scripts/source-lint.sh`：禁 `new Failure(`（豁免 FailureProvider）、bare-string code、`cancel` 命名 | `a0d1208` `83dbf15` |
| 本機 + CI 統一 gate | ✅ | `scripts/ci-checks.sh`（fast/full 雙模式）+ pre-commit（fast）/ pre-push（full）+ `.github/workflows/ci.yml`；本機與 CI 跑同一支腳本，不漂移 | `f621f61` `8088a9d` `b86e0f5` |
| §4 Phase 6：PreToolUse hook | ✅ | `.claude/hooks/pre-tool-edit.py` + `settings.json`，寫的當下攔 4 個 pattern；刻意不攔 `throw`（合法 guard throw 會誤報） | `83dbf15` |
| §3-D / Phase 4：agent reference loading | 🟡 | `session-init.sh` 注入 must-read（B1）✅；`coding-style` / `code-review` skill 強制載入 ⬜ → ✅ 由 Phase H 關閉（見下） | `19f5d45` |
| §3-E / Phase 5：lessons 三類模板 | ✅ | `tasks/lessons.md`（模板早已在用，新增 3 條皆含落地欄位） | `a0d1208` `83dbf15` `19f5d45` |
| §3-B / Phase 3：API contract（對齊 spec） | ✅ | error 改 RFC 9457 ProblemDetails（單一 helper `KeyLifecycle/Http/ApiProblem.cs`：type/title/status/errorCode/traceId）+ `CreateApiKeyResponse.truncatedKey`；functional step 改鎖 RFC 9457 wire contract（一改鎖住所有失敗場景含 @ignore）+ truncatedKey 斷言。綠＋故意紅驗證（改回 `{error}` → 場景紅） | （本次 Phase 3 commit） |
| §9.4 Phase A：協調憲章 | ✅ | `docs/adr/adr-007-process-governance.md`（governance ADR）+ `docs/orchestration.md`（模型分級路由表 / executor contract / 全域停止條件 / checkpoint schema 指針 / token 節約原則）+ `tasks/_templates/checkpoint.md`（交接模板）+ `AGENTS.md`（非 Claude harness 薄入口）+ `tasks/archive/phase-a-spec.md`（可重派指令包）；executor＝Sonnet、orchestrator review 修 1 處簡體字（見 lessons [correction]）；`adr-lint.sh` 綠 + 故意紅驗證通過 | `d8a006b` |
| §9.4 Phase B：學習迴圈機械層重設計（O-5） | ✅ | `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`（orchestrator 起草）+ `session-init.sh` 重寫（session_id marker 去重、`### [` 錨點取最後 8 條、計數指針）+ 兩個 post hook 移除 pending flagging（scrubbing 未動）+ `scripts/hook-smoke.sh` 接入 ci-checks **fast+full**（orchestrator 裁決維持 fast ⊂ full）+ `.gitignore` `.claude/*.marker` + `pending-lessons.jsonl` 刪除（158+ 條最終 triage 記於 ADR-008 Context：0 條值得轉 lesson）；executor＝Sonnet；hook-smoke 綠＋故意紅驗證通過 | （本次 Phase B commit） |
| §9.4 Phase C：驗證矩陣（O-6） | ✅ | `docs/verification-matrix.md`：22 行主表（13 架構測試 / 3 source-lint / adr-lint / format / 4 PreToolUse / hook-smoke / BDD wire / SharedKernel.Tests / AI review 2 級 / 人工 ADR checklist）+ 「無防線區塊」誠實列 10 條規則無機械化（coverage 80%、`.Value` 檢查、cancel 傳播、效能門檻等）；executor＝Sonnet；orchestrator 審校消解 4 項並行時序落差 + 修 1 處簡體字 | `48be025` |
| Phase D：禁簡體 lint（ADR-009） | ✅ | 規則落點進 repo + `scripts/zh-lint.sh`（OpenCC 字表 vendored 4013 條、variant 白名單、行內豁免），接入 fast+full；首跑清償 8 處歷史欠債（含 CLAUDE.md 外科手術 commit `adb4fd8`）；同日三次事故（2 executor + orchestrator 本人）實證人工檢出不可靠 | `12a3967` |
| Phase D：#36 中央套件管理（關閉 #6/#19）＋**協調憲章 DoS (a) 實戰驗收** | ✅ | 冷啟動 Sonnet executor 僅憑「讀 §8.5 + `docs/orchestration.md`」接手：正確選題（#36 一石三鳥）、`ci-checks.sh full` 全綠、checkpoint 合規、誠實回報範圍外異常（GEMINI.md 刪除）— **憲章自轉驗收通過**。`Directory.Packages.props` 統一 18 套件版本 | `1dc717b` |
| Phase D：housekeeping（#35 / #37 / D-3 / D-4） | ✅ | #35 `Rotating` 修正；#37 裁決＝刪除 GEMINI.md 空殼（現實對齊 ADR-001 inventory）；D-3 裁決＝arch-flow 產物與 skill 全部 gitignore；D-4 裁決＝既有 M 改動一併落地（`1dc717b` `a99ca6b`）；lessons 新增 XML `--` 註解陷阱 [info] | （本 housekeeping commit） |
| Phase E：規範文件可發現性接線（ADR-010）+ `.editorconfig` 誤報勘誤 | ✅ | `docs/adr/adr-010-norm-doc-discovery-wiring.md`（`docs/orchestration.md` / `verification-matrix.md` / `checkpoint.md` / `AGENTS.md` 未被自動載入面提及的缺口正式化為治理規則）+ `CLAUDE.md`「Orchestration & Verification」指針小節（新增）+ `session-init.sh` must-read 追加一行 + `scripts/hook-smoke.sh` 斷言同步；`docs/verification-matrix.md`（主表第 11 項／無防線區塊「命名慣例」／審校紀錄）與本檔 §8.3 更正「repo 無 `.editorconfig`」誤報為「`backend/.editorconfig` 存在，僅含 2 檔 whitespace 豁免」，裁決狀態不變；executor＝Sonnet；`adr-lint.sh` / `hook-smoke.sh` / `zh-lint.sh` 綠＋故意紅驗證通過 | （本次 Phase E commit） |
| Phase F：命名規則機械化（ADR-011） | ✅ | `docs/adr/adr-011-naming-rules-editorconfig-enforcement.md`（`backend/.editorconfig` `dotnet_naming_*` severity=error + `dotnet_diagnostic.IDE1006.severity=error` 隱藏開關 + `backend/Directory.Build.props` 新建 `EnforceCodeStyleInBuild=true`）+ `backend/tests/.editorconfig`（新建，Async 後綴 carve-out）；首次全掃（`dotnet build` + `dotnet format --verify-no-changes`）0 違規，既有程式碼無需 rename；`docs/verification-matrix.md`（主表第 11 項改指 ADR-011／無防線區塊「命名慣例」行標✅移出）與本檔 §8.3「`dotnet format` 權威來源模糊」條目關閉；executor＝Sonnet；`adr-lint.sh` 綠＋故意紅驗證通過（`dotnet build` 紅、`dotnet format --verify-no-changes` 亦紅，兩個 gate 皆紅） | （本次 Phase F commit） |
| Phase H：skill must-read 強制（§3-D 最後殘項） | ✅ | `coding-style/SKILL.md` 新增 Phase 2.5「Project Must-Read」：偵測到 .NET / C# 任務時強制讀 `CLAUDE.md` §0 起、`.claude/references/{dotnet,general}/*.rule.md`、`docs/adr/` 內逐檔判定 `## Status` 為 `Accepted` 的 ADR（動態描述，不硬編編號清單）；Phase 2 補 stack 目錄 skip-if-missing 明文化。`code-review/SKILL.md` Phase 2 新增「Both modes — project must-read」段（Self / PR 兩模式皆強制）；Phase 3 Step 1 補 skip-if-missing 明文化。兩份 SKILL.md 皆只放指針（CLAUDE.md §0 / ADR Status 判定流程），未複寫規則內容；executor＝Sonnet | （本次 Phase H commit） |
| Phase I：外部借鏡採用 P1+P2+P3（§10）＋ TBD 分支轉換 | ✅ | 階段 1（P1）：`.claude/hooks/post-edit-validate.sh`（PostToolUse Edit\|Write，4 類副檔名寫後語法驗證）。階段 2（P2）：`scripts/machinery-check.sh`（settings/hooks/pointer 自體健檢，fail-loud），接入 `ci-checks.sh` fast+full；順手修正 `pre-tool-edit.py` 缺可執行位元、`__pycache__/` 補進 `.gitignore`。階段 3（P3）：`docs/adr/adr-012-charter-amendments-external-adoption.md`（unverified_success 條款關閉 O-8、並行派工規則、checkpoint 加「已嘗試且失敗的方法」欄、冷啟動標準 prompt、TBD 分支紀律）+ `docs/orchestration.md`（新 §1.5 / §2 第 5 條 / §6 / §7）+ `tasks/_templates/checkpoint.md`。階段 0（TBD 轉換）首次嘗試卡 add/add merge 衝突（main 經 PR #1 squash 合入同源不同 commit 內容）→ 依 §10.3 補充裁決（2026-07-05：衝突檔取 hardening 版，前提「main 於 PR #1 後零獨立編輯」經雙重驗證成立）完成合併 `5647b21`；合併後首個 CI 紅（machinery-check 指針檢查未豁免 gitignored 的 `settings.local.json`，本機有檔所以本地全綠、CI checkout 無檔轉紅）→ 依 ADR-012 (e)「CI 紅最高優先」即修 `bb2bcfc`（`git check-ignore` 豁免 + 矩陣 15b 同步），CI 轉綠（run 28725618658）；hardening 分支已退役（remote+local 刪除）。各階段驗證：`adr-lint.sh` / `zh-lint.sh` / `ci-checks.sh fast+full`（含 machinery-check）皆綠＋故意紅驗證通過；executor＝Sonnet | `8a03dc9` `d1ee08d` `d756e50` `56ff07d`（經 merge `5647b21` 併入 main）+ `bb2bcfc` |

### 8.3 仍開環（接續 §3 / §4 未關閉項）
- **§3-C / Phase 1**：把 governance（ADR 為唯一通道、同 commit 同步、lessons 必落地）拆成正式 ADR — ✅ 已由 `docs/adr/adr-007-process-governance.md` 關閉（2026-07-04，見 §8.2 Phase A 行）。
- ~~**§3-D 殘項**：`coding-style` / `code-review` skill 的 must-read 強制（B1 注入已做，skill 端尚未）。~~ ✅ 2026-07-05 關閉（Phase H）：兩份 SKILL.md 皆已新增本專案強制載入段（.NET/C# 觸發 CLAUDE.md §0 + Accepted ADR 動態判定）+ stack 目錄 skip-if-missing 明文化 — 詳見 §8.2 Phase H 行。
- ~~**CI 休眠**：repo 尚未上 GitHub；push 後需確認 `ci.yml` 首跑綠並設為 main required status check。~~ ✅ 2026-07-04 關閉（Phase G）：repo 上線 `https://github.com/jame2408/api-protection`（public，使用者裁決維持 public、todo #9 前瞻修正、不重寫歷史）；`main` + 本分支已 push；PR #1 觸發 `ci.yml` 首跑**綠**（`build-test` pass, 59s，run 28706977987）；`build-test` 已設為 main required status check（strict=false，最小保護）。
- ~~**既有 drift**：todo #19（FluentAssertions）、#35（`ROTATING` 殘留）~~ ✅ 2026-07-04 全數關閉（#19 由 `Directory.Packages.props` 根治、#35 已修正、#6/#36/#37 順帶關閉）。
- ~~**禁簡體無機械化防線**~~（2026-07-04 新增；同日兩度驗證必要性）：Phase A review 攔下「执行」、Phase C review 攔下「确定」— 且 orchestrator 手寫掃描字表兩度漏字、grep 多位元組字元類還有 byte-match 誤報陷阱。<!-- zh-lint:allow：本行刻意引用違規字元 --> ✅ **同日關閉**：`docs/adr/adr-009-*.md` + `scripts/zh-lint.sh`（OpenCC 字表 vendored + variant 白名單 + `zh-lint:allow` 行內豁免），接入 ci-checks fast+full；首跑即抓到 8 處人工掃描全數漏掉的真實簡體字（含 CLAUDE.md、api-spec、design-doc）。
- ~~**`dotnet format` 權威來源模糊**~~（2026-07-04 新增，Phase C 發現；2026-07-04 勘誤）：`backend/.editorconfig` 存在，僅含 2 檔 `generated_code` whitespace 豁免；style/naming 規則未定義，格式 gate 對應不到任何 CLAUDE.md/ADR 條文，權威來源仍為工具預設。✅ **2026-07-04 關閉（Phase F）**：`docs/adr/adr-011-naming-rules-editorconfig-enforcement.md` 把命名規則落點定為 `backend/.editorconfig` `dotnet_naming_*` + `EnforceCodeStyleInBuild`，權威來源明確指向 ADR-011 + `naming.guide.md`；whitespace 規則維持工具預設（不在模糊指控範圍內，本就未被要求對應特定條文）。
- **低優先開環（觀察，非阻塞）**：zh-lint 只掃 `git ls-files`（index），新檔在 `git add` 前的工作期不可見 — commit gate 不受影響（staged 即可見），若要擴大掃描範圍到 untracked 檔屬 ADR-009 範圍變更，須開新 ADR。（2026-07-04，Phase F 執行中實際發生一次）

### 8.4 防線層次現況

```
寫的當下（PreToolUse hook + session-init must-read）
  → commit 前（pre-commit fast：format + adr-lint + source-lint）
  → push 前（pre-push full：+ build + 11 架構測試 + BDD）
  → CI（同 full，待 GitHub）
```

最內層（寫的當下）已補上；唯一休眠的是 CI（待 repo 上 GitHub）。涵蓋 9 類 CLAUDE.md / ADR 規則：BC 隔離、Repository raw return、Handler 必回 Result、ILogger 邊界、Handler/Repository/FailureCodes 命名、Failure shape lock、禁 `new Failure(`、bare-string code、`cancel` 命名。

### 8.5 Resume Checkpoint — 已遷出

> 續接入口已遷至 `tasks/checkpoint.md`（單一權威來源，欄位比照 `tasks/_templates/checkpoint.md`），依 `docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (c)。
> 冷啟動 prompt 見 `docs/orchestration.md` §6；本節之前版本的完整交接內容見 git log。
> 本節不再更新——新交接內容一律寫入 `tasks/checkpoint.md`。

---

## 9. 多模型協調層盤點（2026-07-04）

> 視角轉換：§1–§8 解決「規範 vs 程式碼」的 drift；本節解決「協調者退場後，loop 能否由任意模型（Sonnet / Opus / 非 Claude harness）接手且產出品質一致」。盤點方法：loop-engineering skill Phase 1–2；大範圍掃描由 Sonnet subagent 執行，關鍵事實已逐一覆核。

### 9.1 防線可攜性分層（盤點結論）

| 層 | 機制 | 換模型仍有效？ | 換 harness（如 Codex）仍有效？ |
|---|---|---|---|
| 寫的當下 | `pre-tool-edit.py`（PreToolUse）、`session-init.sh` must-read 注入 | ✅ | ❌ Claude Code 專屬 |
| commit 前 | `scripts/git-hooks/pre-commit` → `ci-checks.sh fast` | ✅ | ✅ |
| push 前 | pre-push → `ci-checks.sh full`（build + 13 架構測試 + BDD） | ✅ | ✅ |
| CI | `ci.yml` → 同一支 `ci-checks.sh full` | ✅ | ✅（**休眠**：無 remote） |

**核心判斷**：產出一致性主要靠第 2–4 層機械 gate + 任務切小 + BDD 規格明確，這三者皆 harness-agnostic；第 1 層只是「提早失敗」的加速器，失去它不破壞一致性，只增加來回成本。因此跨模型一致性的地基已存在，缺的是協調層本身（見 9.2）。

### 9.2 缺口清單（O 系列）

| # | 缺口 | 斷在哪一段 |
|---|---|---|
| O-1 | **協調層無 artifact**：任務怎麼切、派給哪一級模型、監督協定、Fable 退場後誰按什麼規則調度 — 只存在於對話與人的記憶 | 無落地 |
| O-2 | **Executor contract 不存在**：executor session 的義務（進度與實作同 commit、誠實申報 blocker／不確定處、何時必須停）未成文，換一個模型就換一套行為 | 無落地 |
| O-3 | **全域停止條件缺失**：BDD cycle 有局部停止規則（一次一個 @ignore、Green before commit），但無全域規則（同一測試連紅 N 次→停、規格模糊→停+問、超出任務邊界→停） | 無訊號＋無落地 |
| O-4 | **Checkpoint/handoff 是散文慣例非 schema**：§8.5 是好範例，但無模板可複製；交接品質取決於寫的人自覺，弱模型寫不出同等品質 | 無機械化 |
| O-5 | **學習迴圈積壓 + token 無界成長**：`pending-lessons.jsonl` 158 條未 triage；`session-init.sh` 每 session 全量注入 `lessons.md`，隨條目增加 token 成本線性上升 | 學習回寫斷 |
| O-6 | **驗證矩陣缺「執行者」欄**：哪些驗證用腳本、哪些需 AI review、AI review 用哪級模型（明文排除 Fable 級）— 未定義；multi-agent review 至今是 ad-hoc | 無決策通道 |
| O-7 | **Tessl eval harness 未決**：`tessl.json` / `.mcp.json` / `.tessl/` untracked、skill 內模型名已過時（`claude-sonnet-4-6` 等）；其 compare-skill-model-performance 能力與「跨模型一致性」目標高度相關，但未納入制度也未移除 | 無決策 |
| O-8 | ~~**Subagent 事實覆核未機械化**：本次盤點 subagent 宣稱 repo 無 `GEMINI.md`，實際存在於 `.claude/references/general/`。CLAUDE.md §2 已有「不接受概括」規則，但覆核動作靠 orchestrator 自覺~~ ✅ **2026-07-05 關閉（Phase I / ADR-012 決策 (a)）**：`docs/orchestration.md` §2 Executor Contract 新增第 5 條 unverified_success 條款，成文規定協調者必須親自執行確定性檢查才能將 executor／subagent 的宣稱升級為已驗證 | ~~無機械化~~ → `docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (a) + `docs/orchestration.md` §2 第 5 條 |

承接 §8.3 未關閉項（不重列）：governance ADR、skill must-read、CI 首跑、todo #19（FluentAssertions）、#35（ROTATING）。

### 9.3 裁決紀錄（2026-07-04 使用者裁決）

- **D-1 跨 harness 範圍**：✅ **近期會用，現在就建** — Phase A 包含 `AGENTS.md` 入口（薄指針，不複寫規則）。
- **D-2 Tessl 處置**：✅ **擱置，維持 untracked** — 不進 git、不寫進驗證矩陣；協調憲章落地後真需要跨模型 skill eval 再議。
- **D-3 未追蹤產物**：✅ 2026-07-04 裁決 — arch-flow 產物與 skill 目錄全部 gitignore（可重產、不入制度）。
- **D-4 既有改動歸屬**：✅ 2026-07-04 裁決 — 一併提交（`1dc717b` todo.md、`a99ca6b` CLAUDE.md）。

### 9.4 提議 Phase（Sonnet 5 執行、Fable 監督起步；每 Phase 獨立可中斷）

- **Phase A — 協調憲章（最高優先：這是 Fable 退場後唯一能留下的東西）**：正式 ADR（含 §8.3 懸置的 governance ADR，或拆二）+ `docs/orchestration.md`：模型分級路由表（簡單批量→sonnet/opus；AI review 分級明文**排除 Fable 級**）、executor contract（O-2）、全域停止條件（O-3）、checkpoint schema 模板（O-4，落 `tasks/_templates/`）、token 節約原則（有界注入、指針不複寫、checkpoint 優先於重讀）。
  **狀態（2026-07-04）：✅ 已落地** — 五項交付物 + `tasks/archive/phase-a-spec.md`（可重派指令包）；executor＝Sonnet，orchestrator review 後修正 1 處簡體字並同 commit 落地。詳見 §8.2 Phase A 行。下一步：Phase B（見下），可由新 session 直接依本節接手。
- **Phase B — 學習迴圈減壓（O-5）**：✅ **2026-07-04 完成**，設計定案於 ADR-008（flagging 管線退役而非修補、marker 去重、注入上限 8 條、hook-smoke 防再次靜默死亡），落地見 §8.2 Phase B 行。O-5 關閉。
- **Phase C — 驗證矩陣（O-6）**：✅ **2026-07-04 完成**，`docs/verification-matrix.md`（Tessl 依 D-2 不列入），落地見 §8.2 Phase C 行。O-6 關閉；矩陣揭露的 10 條「無防線」規則為既知現況登記，非新開環。
- **Phase D — 殘項**：skill must-read（§8.3）、todo #19（建議 `Directory.Packages.props` 一次解，見 todo #36）、#35。

**驗收（Definition of Success）**：(a) 換一個全新 session、指定 Sonnet 5、只給「讀 §9.4 + 協調憲章」的 prompt，能正確接手一個 Phase 並產出合規 checkpoint；(b) session-init 注入量有上限且可 grep 驗證；(c) 每條新機制過「綠＋故意紅」。

---

## 10. 外部借鏡：`zeuikli/claude-code-workspace` 採用分析（2026-07-05）

> 使用者指定研究此 repo（152 stars，Claude Code workspace 設定倉庫）並擷取對我們 loop 有幫助的機制。盤點由 Sonnet subagent 完成（60 檔全樹 + 逐機制 verbatim），採用判斷為 orchestrator 分析。原始檔案暫存於 scratchpad `ccw_inventory/` 供複查。

### 10.1 逐項評估

| 它的機制 | 評估 | 理由 |
|---|---|---|
| `post-edit.sh`：依副檔名寫後語法驗證（`bash -n` / `json.load` / `py_compile`） | ✅ **採用（P1，擴充版）** | 直接對應我們的 NU1015 事故（XML 註解 `--` 使 props 被靜默跳過，見 lessons [info]）— 加上 XML 家族（`.props`/`.csproj`/`.targets`）的 well-formed 驗證，寫的當下就攔 |
| `healthcheck.sh`：機制自體健檢（settings JSON、hooks 可執行位元+語法、指針完整性） | ✅ **採用（P2，裁縫版）** | 我們有過 hook 靜默死亡（awk 事故），但 hook-smoke 只護 session-init；其餘 hooks / settings / 文件指針無「防線的防線」 |
| `core.md` 的 `unverified_success` 閘門：「subagent 自報成功＝中間態，確定性檢查不經 subagent 中介」 | ✅ **採用（P3-a，憲章修訂）** | 正是 O-8 的成文化 — 我們已實踐（GEMINI.md 誤報靠覆核抓到）但未成文；採用後 O-8 關閉 |
| Fan-out 上限、child 不互通、不 self-retry；`isolation: worktree` | ✅ **採用（P3-b，憲章修訂）** | 我們實際踩過並行衝突（B/C 搶 matrix、F/G build 互擾靠人工串行）；成文為並行派工規則：檔案集不相交、動 build 者串行或用 worktree 隔離、fan-out 上限 |
| `HANDOFF.md` 的「嘗試過的方法」表格 | ✅ **採用（P3-c）** | checkpoint 模板缺此欄 — 下個 executor 會重試死路；欄位結構修改依 ADR-007 走 ADR |
| `prompts.md` / `prime`：標準冷啟動 prompt | ✅ **採用（P3-d，一小段）** | 給使用者的固定開場（「讀 §8.5 依 checkpoint 接手」）寫進 orchestration.md，降低每次開 session 的成本 |
| Auto Memory 取代自建 lessons（`Memory.md` 佔位） | ❌ 不採用 | 綁定 Claude Code 官方功能，與 D-1 跨 harness 目標相反；我們的 lessons.md + 有界注入（ADR-008）是 repo 可攜的 |
| Token 數字預算（per-task 4000 / per-session 30000） | ❌ 不採用 | 無量測依據的數字是偽精確；我們的 token 原則（orchestration.md §5）夠用，等有實測再議 |
| `RESOLVER.md` skill 路由、`output-discipline`、agent `.md` 定義檔 | ❌ 不採用 | 非我們痛點；agent 定義檔是 Claude Code 專屬，我們的 phase-spec 指令包模式已覆蓋且 harness 中立 |
| 反面教材（一併記錄） | ⚠️ 引以為戒 | 該 repo 自身有典型 drift：CI 引用不存在的 `memory-pull.sh` 用 `if [ -f ]` **靜默跳過**、RESOLVER 提及多個不存在的 skill、README 聲稱 MIT 但無 LICENSE 檔 — P2 健檢的指針完整性檢查必須 **fail-loud**，不重蹈此轍 |

### 10.2 提議工作包（待使用者裁決）

- **P1**：`.claude/hooks/post-edit-validate.sh`（PostToolUse Edit|Write）：`.sh`→`bash -n`、`.json`→JSON parse、`.py`→`py_compile`、`.props/.csproj/.targets/.xml`→minidom well-formed。需改 `.claude/settings.json` 註冊（經使用者同意）。只做零誤報層級的檢查。
- **P2**：`scripts/machinery-check.sh` 接入 fast gate + CI：settings.json 合法、hooks 可執行+`bash -n`、`pre-tool-edit.py` py_compile、規範文件指針完整性（CLAUDE.md / orchestration / 矩陣引用的檔案必須存在，缺失即紅）。
- **P3**：ADR-012 憲章修訂（單一 ADR 涵蓋 a–d）：(a) unverified_success 條款（關 O-8）；(b) 並行派工規則；(c) checkpoint 模板加「已嘗試且失敗的方法」欄；(d) 冷啟動標準 prompt 段。
- 全部依慣例：綠＋故意紅、矩陣同 commit 同步、executor 實作 + orchestrator review。

### 10.3 裁決紀錄（2026-07-05 使用者裁決）

- **採用項目**：✅ P1 + P2 + P3 全數採用。
- **分支策略**：✅ **Trunk-Based Development，直接寫入 main**（兩人小團隊，PR 流程過重）。連動處置：解除 main 的 required status check（它會擋直接 push）；防線轉為「本機 pre-push full gate（與 CI 同腳本）為主 + CI 於 push-to-main 後跑驗證訊號」；hardening 長命分支併回 main 後退役。分支紀律明文入 ADR-012。
