# 治理規則正式化：ADR 為唯一通道、同步同 commit、lessons 分類、協調憲章納管

> 本 ADR 把散落在 `tasks/process-improvement-plan.md` §3/§4/§8/§9 的治理慣例（哪些檔案有規範效力、規範怎麼改、lessons 怎麼分類）正式化為 Accepted ADR，終結「plan 檔案本身變成隱性規範」的風險。

---

## Status

Accepted (2026-07-04)

同步項目：預期無需修改 `CLAUDE.md` — 其「Architecture Decision Records (ADR)」段與「Self-Improvement Loop」段已陳述本 ADR 的核心規則（ADR 為新規範起點、同步項目同 commit、lessons 觸發條件），本 ADR 只是把它們提升為跨文件的一般性治理原則並補上先前只存在於 plan 檔案的細節（lessons 三類分類、必填落地欄位、`docs/orchestration.md` 納管）。若 review 過程發現與 `CLAUDE.md` 現有文字有實質衝突，應停下並開新提案修正 `CLAUDE.md`，不在本 ADR 內處理。

---

## Context

### 現況

`tasks/process-improvement-plan.md` 目前同時扮演兩種角色：一是「執行紀錄」（§8 落地紀錄、§9 盤點），二是事實上的「規範存放處」。例如：

- §3-C／§8.3 寫著「governance（ADR 為唯一通道、同 commit 同步、lessons 必落地）」— 這句話本身描述一條治理規則，但規則寫在 plan 裡，不是 ADR，沒有 `scripts/adr-lint.sh` 檢驗、沒有 Accepted 狀態、沒有治理條款擋修改。
- `tasks/lessons.md` 的既有條目已經一致使用 `[decision]` / `[correction]` / `[info]` 三種標籤、且每條都有「**落地:**」欄位（見該檔第一批條目），但這個格式從未被任何規範文件明文規定過 — 它是「大家照做但沒人寫下來」的慣例。
- `CLAUDE.md` 的「Architecture Decision Records (ADR)」段已明文：「ADRs that touch reference docs / CLAUDE.md / examples MUST explicitly list "同步項目" ... All sync edits MUST land in the same commit as the ADR.」— 但這條規則的適用範圍寫成「touch reference docs / CLAUDE.md / examples」，沒有涵蓋到 §9.4 Phase A 新引入的 `docs/orchestration.md`。

### 問題嚴重度

- 這不是單一 drift，而是「規範來源不唯一」的結構性風險：任何人（包含 AI agent）修改 `process-improvement-plan.md` 的措辭，事實上就改變了規範，卻不需要通過 ADR review、不需要 `adr-lint.sh`、不留 Accepted 時間戳。
- `docs/orchestration.md`（本次 Phase A 一併產出）一旦存在但未被納管，未來任何人可以直接編輯協調憲章而不開 ADR，重演同一種 drift。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| `tasks/process-improvement-plan.md` | 執行紀錄 / backlog / 盤點筆記 | ❌ 不規定其格式，但規定它不得作為規範終局來源 |
| ADR（`docs/adr/adr-*.md`） | 規範的唯一權威來源 | ✅ 本 ADR 的核心對象 |
| `CLAUDE.md` | 規範的彙整 / AI agent 讀取入口 | ✅（僅治理面：修改須源自 Accepted ADR） |
| `docs/orchestration.md` | 協調憲章（模型分級、executor contract、停止條件） | ✅ 納入 ADR 管轄 |
| `tasks/lessons.md` | 學習紀錄，非規範 | ✅（僅格式面：三類分類 + 落地欄位） |

---

## Decision

### 1. 規範修改唯一通道 = ADR

任何跨檔案生效、會被 AI agent 或人類開發者當作「規則」遵守的工程規範（架構規則、測試策略、CI gate 行為、agent 協調規則、lessons 格式要求），其新增或修改必須以新 ADR 提出，並在 Accepted 後才生效。

```diff
- tasks/process-improvement-plan.md §8.3:
-   "governance（ADR 為唯一通道、同 commit 同步、lessons 必落地）— 未做"
+ docs/adr/adr-007-process-governance.md（本 ADR）:
+   Accepted (2026-07-04) — 具備 Status / 治理條款 / adr-lint 驗證
```

`tasks/process-improvement-plan.md` 保留作為執行紀錄與 backlog，但其中提到的「規則」一旦要生效，必須有對應的 Accepted ADR 作為權威來源；plan 檔案本身的措辭不構成規範。

### 2. 規範與其衍生物必須同 commit 同步

`CLAUDE.md`「Architecture Decision Records (ADR)」段既有規則（ADR 的「同步項目」須同 commit）正式擴大適用範圍：不限於 ADR 本身，任何規範文件（`CLAUDE.md`、`docs/orchestration.md`、ADR）修改其內容時，只要影響到其他文件中對同一規則的引用或範例（`.claude/references/**/*.rule.md`、`AGENTS.md` 指向的段落、`tasks/_templates/checkpoint.md`），該影響必須在同一個 commit 內一起修正，不得分成「先改規範、之後再補衍生物」兩個 PR。

### 3. `tasks/lessons.md` 三類分類 + 必填「落地」欄位

每筆 lessons 條目標題必須以下列三個分類標籤之一開頭：

- `[decision]` — 架構或技術取捨（含替代方案為何被否決）
- `[correction]` — 使用者糾正或 agent 自我修正
- `[info]` — 環境、工具、既成事實的記錄（非決策）

每筆條目必須包含「**落地:**」欄位，內容指向實際變更的檔案路徑（測試檔、腳本、程式碼位置），不得只寫抽象敘述而無對應產出。此規則是既有慣例的正式化 — `tasks/lessons.md` 現有條目（如「Service 必回 Result」「HTTP boundary helper 放 BC 內」）已全數符合，本 ADR 不要求追溯修改既有條目，僅要求未來新增條目遵守。

### 4. `docs/orchestration.md` 納入 ADR 管轄

`docs/orchestration.md`（協調憲章）與 `CLAUDE.md` 同等級：修改其任一章節（模型分級路由表、executor contract、全域停止條件、checkpoint schema 指針、token 節約原則）必須先開新 ADR，比照 `CLAUDE.md` 的既有治理層級。`docs/orchestration.md` 本文不得複寫 ADR 或 `CLAUDE.md` 的規則內容，只放指針與協調層特有的補充規則（見 ADR §「不在本 ADR 範圍」）。

### 不在本 ADR 範圍

- 本 ADR 不規範 `docs/orchestration.md`、`AGENTS.md`、`tasks/_templates/checkpoint.md` 的具體內容細節 — 那些內容本身由 Phase A 的其他交付物承載，本 ADR 只確立「它們一旦存在，改動要走 ADR / 同步同 commit」的治理層規則。
- 本 ADR 不追溯修改 `tasks/lessons.md` 既有條目的格式，只約束未來新增條目。
- 本 ADR 不變更 `scripts/adr-lint.sh` 的檢驗邏輯本身。

### 5. 本 ADR 接受時的同步項目

- `docs/orchestration.md`（新檔，Phase A 同批產出）：§「Checkpoint schema」段落須註明「修改本檔案任一章節須先開新 ADR」，指向本 ADR。
- `AGENTS.md`（新檔，Phase A 同批產出）：作為薄入口，不重複本 ADR 的規則內容，只指回 `CLAUDE.md` 與 `docs/orchestration.md`。
- 預期 **無需修改 `CLAUDE.md`**（見 Status 段落說明）。

---

## Rationale

### 為什麼選擇「ADR 為唯一通道」而不是「plan 檔案即可」

`tasks/process-improvement-plan.md` 沒有 lint、沒有 Accepted 狀態鎖定、沒有治理條款擋修改 — 它是給人類/agent 讀的敘事文件，適合記錄「怎麼想到這個決定」，不適合作為「這條規則現在生效」的權威來源。把兩者混在一起，會讓「更新執行紀錄」和「修改規範」變成同一個動作，稀釋 review 時的警覺。

### 為什麼不擴張到強制重寫既有 lessons 條目

既有 `tasks/lessons.md` 條目已經自然符合三類分類與落地欄位慣例（見 Context）。追溯性重寫只會製造不必要的 diff 與 git blame 雜訊，且既有條目本來就沒有違反本 ADR 的意圖 — 它們是這條規則的證據，不是需要修正的對象。

### 為什麼 `docs/orchestration.md` 要比照 `CLAUDE.md` 的治理層級而不是自成一套

若協調憲章有自己一套較寬鬆的修改規則（例如不需要 ADR），就會重演本 ADR 想終結的問題：兩份「規範地位」文件、兩套修改門檻，drift 只是換了個地方發生。統一到「所有規範文件修改都走 ADR」是最小規則集。

---

## Consequences

### Positive

- 規範來源單一化：任何人想知道「現在的規則是什麼」，只需要看 Accepted ADR 集合 + `CLAUDE.md`，不需要爬梳 plan 檔案的歷史措辭。
- `docs/orchestration.md` 從一開始就被納管，不會出現「先寫協調憲章、之後才想到要不要開 ADR」的時間差漏洞。
- lessons 格式正式化後，未來的 lessons tooling（例如 §9.2 O-5 的 pending-lessons triage）有明確的 schema 可以校驗。

### Negative / Trade-offs

- 未來任何協調規則的微調（即使只是措辭）都需要走 ADR 流程，比直接編輯 plan 檔案慢。
  - Mitigation: ADR 的沉沒成本主要在初次撰寫；`docs/adr/_template.md` + `scripts/adr-lint.sh` 已把格式成本壓到最低，且治理規則本身變動頻率低（本 ADR 是這類規則第一次形成）。
- `tasks/process-improvement-plan.md` 與 ADR 集合之間可能出現「plan 說要做、ADR 還沒開」的暫時性不一致（如本 ADR 產生前，§8.3 已經用文字描述了這條規則）。
  - Mitigation: §8.3／§9.4 在本 ADR 落地後於同一批交付物中回寫「已由 ADR-007 關閉」，避免兩份文件各自宣稱權威。

---

## Alternatives Considered

### Alternative A：維持現狀，治理規則留在 `process-improvement-plan.md`

Rejected. 這正是本 ADR 想終結的風險本身 — plan 檔案沒有 lint、沒有 Accepted 鎖定，等同於「規範可被靜默修改」，與 ADR-004/ADR-005/ADR-006 建立的治理標準不一致。

### Alternative B：把治理規則直接寫進 `CLAUDE.md`，不另開 ADR

Rejected. `CLAUDE.md` 是規範的「彙整與入口」，其修改本身應該源自 ADR（現有 ADR 章節已隱含這個期待：「ADRs that touch ... CLAUDE.md ... MUST explicitly list 同步項目」）。若治理規則本身跳過 ADR 直接寫進 `CLAUDE.md`，會讓「修改 CLAUDE.md 需要 ADR」這條規則自我豁免，邏輯不自洽。

### Alternative C：為 `docs/orchestration.md` 建立獨立的、比 ADR 更輕量的治理機制（例如版本號 + changelog）

Rejected. 引入第二套治理機制會產生「哪些文件受 ADR 管、哪些受輕量機制管」的認知負擔，且違反「單一真相來源」的驗收要求（本規格 §驗收）。協調憲章的變動頻率預期不高於一般架構決策，沒有證據顯示需要更輕量的機制。

---

## Implementation Rules

1. 任何預期被當作規則長期遵守的工程規範（架構、測試策略、CI gate、agent 協調、lessons 格式），只能透過 Accepted ADR 新增或修改；`tasks/process-improvement-plan.md` 的文字本身不構成規範來源。
2. 規範文件（`CLAUDE.md` / `docs/orchestration.md` / ADR）修改時，若影響其他文件對同一規則的引用或範例，該影響必須在同一 commit 內一起修正。
3. `tasks/lessons.md` 新增條目必須以 `[decision]` / `[correction]` / `[info]` 三者之一開頭，並包含「**落地:**」欄位指向實際變更的檔案路徑；既有條目不追溯修改。
4. `docs/orchestration.md` 的修改比照 `CLAUDE.md` 的治理層級，須先有 Accepted ADR。
5. `docs/orchestration.md` 與 `AGENTS.md` 不得複寫本 ADR 或 `CLAUDE.md` 的規則內容，僅可放指針（檔案 + 段落標題）。
6. **驗收**：`tasks/process-improvement-plan.md` §8.3 的 governance ADR 待辦項須標記由本 ADR 關閉；§9.4 Phase A 項須標記進度。以下指令用於確認沒有第二份文件完整複製本 ADR 的規則內容（僅允許指針引用，例如「見 ADR-007」）：

   ```bash
   git --no-pager grep -n '三類分類' -- docs/ CLAUDE.md AGENTS.md ':!docs/adr/adr-007-process-governance.md'
   # 預期 0 命中（其他文件僅可指針引用「見 ADR-007」，不得複寫分類定義本身）
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
