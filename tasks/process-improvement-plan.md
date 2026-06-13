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
| §3-D / Phase 4：agent reference loading | 🟡 | `session-init.sh` 注入 must-read（B1）✅；`coding-style` / `code-review` skill 強制載入 ⬜ | `19f5d45` |
| §3-E / Phase 5：lessons 三類模板 | ✅ | `tasks/lessons.md`（模板早已在用，新增 3 條皆含落地欄位） | `a0d1208` `83dbf15` `19f5d45` |

### 8.3 仍開環（接續 §3 / §4 未關閉項）

- **§3-B / Phase 3**：API contract tests（RFC 9457 ProblemDetails、`truncatedKey` wire contract、failure→response mapping）— 未做。
- **§3-C / Phase 1**：把 governance（ADR 為唯一通道、同 commit 同步、lessons 必落地）拆成正式 ADR — 未做。目前散在本 plan，本身就是「plan 變隱性規範」的風險。
- **§3-D 殘項**：`coding-style` / `code-review` skill 的 must-read 強制（B1 注入已做，skill 端尚未）。
- **CI 休眠**：repo 尚未上 GitHub；push 後需確認 `ci.yml` 首跑綠並設為 main required status check。
- **既有 drift**：todo #19（FluentAssertions 8.9.0 違反 `<8.0.0`）、#35（`ROTATING` 殘留）。

### 8.4 防線層次現況

```
寫的當下（PreToolUse hook + session-init must-read）
  → commit 前（pre-commit fast：format + adr-lint + source-lint）
  → push 前（pre-push full：+ build + 11 架構測試 + BDD）
  → CI（同 full，待 GitHub）
```

最內層（寫的當下）已補上；唯一休眠的是 CI（待 repo 上 GitHub）。涵蓋 8 類 CLAUDE.md / ADR 規則：BC 隔離、Repository raw return、Handler 必回 Result、ILogger 邊界、Handler/Repository/FailureCodes 命名、禁 `new Failure(`、bare-string code、`cancel` 命名。
