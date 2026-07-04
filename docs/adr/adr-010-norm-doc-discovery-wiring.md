# 規範文件可發現性接線：新規範文件必須同 commit 接上自動載入面

> 本 ADR 終結「寫了規範文件，但沒有任何自動載入面指向它」的隱性開環——`docs/orchestration.md`、`docs/verification-matrix.md`、`tasks/_templates/checkpoint.md` 三份 Phase A/C 產出的規範文件，至今未被 `CLAUDE.md`、`session-init.sh` 或 `AGENTS.md` 提及，一般 session 的 agent 無從得知它們存在。本 ADR 接上指針、並把「新規範文件須同 commit 接線」訂為治理級規則。

---

## Status

Accepted (2026-07-04)

同步項目：`CLAUDE.md`（新增「Orchestration & Verification」指針小節）、`.claude/hooks/session-init.sh`（must-read 段追加一行）、`scripts/hook-smoke.sh`（新增斷言覆蓋新增行）。三者須與本 ADR 同一個 commit 落地。

---

## Context

### 現況

`docs/orchestration.md`（協調憲章）、`docs/verification-matrix.md`（驗證登記表）、`tasks/_templates/checkpoint.md`（交接模板）三份文件已由 Phase A / C 產出並受 `docs/adr/adr-007-process-governance.md` 治理（修改須經 ADR），但「治理」與「可被發現」是兩件事：

- `CLAUDE.md` 全文搜尋不到 `orchestration.md`、`verification-matrix.md`、`checkpoint.md` 任一字串——一般 session 的 agent 只讀 `CLAUDE.md` 與 session-init 注入內容，不會主動列舉 `docs/` 目錄尋找未被提及的檔案。
- `.claude/hooks/session-init.sh` 的 must-read 段（見該檔「Must-read rules reminder」區塊）只提到 `.claude/references/**/*.rule.md`、`docs/adr/` 內 Accepted ADR、`docs/design/api-spec.md`，同樣未提及上述三份文件。
- `AGENTS.md`（非 Claude harness 入口）已經指向 `docs/orchestration.md`（見該檔「協調規則」段），但這是給*非* Claude Code harness 的補償手段；Claude Code session 的主要入口是 `CLAUDE.md` + session-init 注入，兩者都是空的。
- `docs/verification-matrix.md` 與 `tasks/process-improvement-plan.md` §8.3 均宣稱「repo 未建 `.editorconfig`」；實際 `backend/.editorconfig` 存在（僅含 2 檔 `generated_code` whitespace 豁免）。這是獨立的記錄性勘誤，不影響本 ADR 的接線決策，隨本 commit 一併修正（見本規格 §3，不在本 ADR Decision 範圍內）。

### 問題嚴重度

- 這與 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 診斷過的「給人有防線錯覺的死機制，比沒有更糟」同構：三份文件在治理上「存在且受管」，但在發現面上「形同不存在」，會讓依賴它們的協調機制（executor contract、驗證分工、交接品質）在實務上從未被讀取，卻沒有任何訊號顯示這一點。
- 若不建立「新規範文件必須同 commit 接線」的治理規則，這個模式會在未來新增規範文件時重演——每次都要靠人事後發現「怎麼沒人知道這份文件」。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| 文件受 ADR 治理（修改需先開 ADR） | `docs/adr/adr-007-process-governance.md` 已規定 | ❌ 已由 ADR-007 覆蓋，本 ADR 不重複 |
| 文件可被自動發現（session 開工時會被讀到或看到指針） | 本 ADR 的核心對象 | ✅ |
| `docs/orchestration.md` / 矩陣本身的內容 | Phase A / C 的既有產出 | ❌ 不在本 ADR 範圍，內容不動 |
| session-init 注入的上限與 dedup 邏輯 | `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 已規定（session_id marker、lessons 上限 8 條） | ❌ 不在本 ADR 範圍，邏輯不動，只追加一行 must-read 文字 |

---

## Decision

### (a) `CLAUDE.md` 新增「Orchestration & Verification」指針小節

在「Workflow Orchestration」章（`## Workflow Orchestration`）之下、與現有 5 個編號小節（Reference Loading / Plan-First / Subagent Strategy / Self-Improvement Loop / Verification Standards / Demand Elegance）並列，新增一個不編號的指針小節，只放 3–5 行指針，不複寫任何內容：

```diff
 ### 5. Demand Elegance

 - Before presenting a solution, silently evaluate: ...
 - If a fix feels like a "hack," find the root cause ...
 - Avoid over-engineering: elegance means the simplest correct solution ...
+
+### Orchestration & Verification
+
+多模型協調與驗證機制的權威來源不在本檔，只放指針：
+- `docs/orchestration.md` — 多模型協調憲章（模型分級、executor contract、全域停止條件）
+- `docs/verification-matrix.md` — 驗證登記表（哪條規則由什麼機制、在什麼時機、由誰驗證）
+- `tasks/_templates/checkpoint.md` — session 交接模板
+- `AGENTS.md` — 非 Claude Code harness 的薄入口
```

此修改源自本 ADR（滿足 `docs/adr/adr-007-process-governance.md` Implementation Rules 1「規範修改唯一通道 = ADR」），且為使用者發起裁決（滿足 `CLAUDE.md`「Working Agreement」段「Never bulk-rewrite CLAUDE.md. All changes must be scoped, intentional, and initiated by the user.」）。

### (b) `session-init.sh` must-read 段追加一行

`.claude/hooks/session-init.sh` 的 must-read echo 區塊（見該檔「Must-read rules reminder」段）在既有三行指針之後追加一行：

```diff
 echo "- \`.claude/references/dotnet/*.rule.md\` 與 \`.claude/references/general/*.rule.md\`"
 echo "- \`docs/adr/\` 內 Accepted 的 ADR（錯誤處理 / DI / 命名 / wire-format 決策）"
 echo "- \`docs/design/api-spec.md\`（你要碰的 endpoint 章節）"
+echo "- 多模型協調與驗證機制：\`docs/orchestration.md\`、\`docs/verification-matrix.md\`"
 echo ""
```

`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`「不在本 ADR 範圍」明文列出「不改 must-read 段的文字內容（只改注入頻率）」——這句話約束的是 ADR-008 本身不動這段文字，不是凍結它不得再被修改。ADR-008 Status 段同時聲明「`tasks/process-improvement-plan.md` §8.2/§8.3/§9.4 回寫」與其治理範圍，並未宣告 must-read 文字為凍結內容；本 ADR 是修改 must-read 文字內容的合法新 ADR 通道，銜接方式即為本節。

### (c) 接線規則（治理級）：新規範文件必須同 commit 接上至少一個自動載入面

未來任何新增的、會被 AI agent 或人類開發者當作規則遵守的規範級文件，必須在**同一個 commit** 內接上至少一個自動載入面——`CLAUDE.md` 指針小節、`session-init.sh` must-read 段、或 `AGENTS.md` 指針——三者擇一或多個皆可，缺一律視為交付未完成。「寫了文件沒人知道」本身就是一個開環，與內容本身是否正確無關。

```diff
- 新增 docs/some-new-spec.md（規範性文件）
- commit 訊息：「feat: 新增 some-new-spec」
- （後續某次 review 才發現沒人讀過它）
+ 新增 docs/some-new-spec.md（規範性文件）
+ 同一 commit：CLAUDE.md 或 session-init.sh 或 AGENTS.md 三者至少一處新增指針
+ commit 訊息：「feat: 新增 some-new-spec + 接線 CLAUDE.md 指針」
```

### 不在本 ADR 範圍

- 不改變 `docs/orchestration.md`、`docs/verification-matrix.md` 本身的內容（矩陣的 `.editorconfig` 勘誤是獨立的記錄性修正，隨本 commit 一併處理，非本 ADR 的 Decision 產物）。
- 不動 session-init 注入的上限與 dedup 邏輯——`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 規則不變，本 ADR 只追加一行文字，不改注入頻率、去重機制或 lessons 抽取邏輯。
- 不規範 `docs/orchestration.md` / `docs/verification-matrix.md` / `tasks/_templates/checkpoint.md` 本身的修改門檻——那已由 `docs/adr/adr-007-process-governance.md` Implementation Rules 4 覆蓋。
- 不追溯要求已存在的規範文件之外的其他文件（例如既有的 `.claude/references/**/*.rule.md`）補接線——它們已經是 must-read 段既有指針的對象，不受影響。

---

## Rationale

### 為什麼用「指針小節」而不是把三份文件的內容摘要進 `CLAUDE.md`

摘要會製造第二份權威來源——一旦 `docs/orchestration.md` 或矩陣更新，`CLAUDE.md` 裡的摘要就可能漂移，重演 `docs/adr/adr-007-process-governance.md` Context 段描述的「規範來源不唯一」風險。指針小節維持「`CLAUDE.md` 是入口、其他文件是正典」的既有架構（`AGENTS.md`「規則正典」段已是同一模式）。

### 為什麼追加到 session-init must-read 而不是只改 `CLAUDE.md`

`CLAUDE.md` 指針只在 agent *主動讀取*整份 `CLAUDE.md` 時才會被看到；session-init 是每 session 保證注入一次的機械化面，兩者互補而非重複——`CLAUDE.md` 面向「回頭查閱」，must-read 面向「開工前保證看到」。只做一個會留下另一半的發現缺口，這正是本 ADR 想關閉的問題本身。

### 為什麼不擴張成「重新設計整個規範發現架構」

現有架構（`CLAUDE.md` 入口 + `AGENTS.md` 薄入口 + session-init 機械注入）已經是 `docs/adr/adr-007-process-governance.md` 與 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 兩份 ADR 定型的設計，運作良好（`hook-smoke.sh` 已驗證注入邏輯）。本次問題是「三份文件恰好漏接」的執行缺口，不是架構缺陷；用既有架構補三個指針，成本遠低於重新設計，也不違反「Minimal Blast Radius」原則。

---

## Consequences

### Positive

- 一般 Claude Code session 開工時，must-read 注入即會看到協調憲章與驗證矩陣的存在，不再需要人工提醒或事後發現。
- `CLAUDE.md` 讀者（人類或 agent 主動查閱時）可從「Workflow Orchestration」章直接找到協調層文件，不需要知道 `docs/` 目錄結構。
- 治理級接線規則（Decision (c)）確立後，未來新增規範文件時，交付定義本身就包含接線，不會再有「寫了文件沒人知道」的隱性開環累積。

### Negative / Trade-offs

- `session-init.sh` must-read 段每次 session 多一行輸出，略增每 session 的固定 token 成本。
  - Mitigation: 這是與既有三行指針同等級的固定成本（每 session 一次，不隨內容增長），遠低於 ADR-008 已解決的「無界成長」風險；且指向的兩份文件對協調層工作是必要上下文，省下的來回成本更高。
- Decision (c) 的「接線規則」是治理級承諾，需要靠人 review 或 orchestrator review 把關，沒有機械化 lint 強制執行。
  - Mitigation: 比照 `docs/verification-matrix.md` 對「人工類」規則的處理方式——明文列為 review checklist 項目而非假裝有腳本防線；未來若接線遺漏頻繁發生，可另開 ADR 討論是否值得機械化（例如 grep 新增 `docs/*.md` 是否同 commit 出現在 `CLAUDE.md`/`session-init.sh`/`AGENTS.md` 的 diff 中）。

---

## Alternatives Considered

### Alternative A：只改 `AGENTS.md`，不動 `CLAUDE.md` 與 `session-init.sh`

Rejected. `AGENTS.md` 是給非 Claude Code harness 的補償手段（見該檔開頭「薄入口，給非 Claude Code harness 的 AI agent」），Claude Code session 的主要入口是 `CLAUDE.md` + session-init 注入。只改 `AGENTS.md` 等於沒有解決「一般 session 的 agent 不知道它們存在」這個原始問題陳述本身。

### Alternative B：把 `docs/orchestration.md` 與矩陣的全文內容直接注入 session-init（比照 lessons 抽取邏輯）

Rejected. 兩份文件篇幅遠大於 lessons 單條條目，全文注入會顯著推高每 session 固定成本，且與 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` Decision 2「注入上限 8 條 lessons」建立的「注入預算需有界」原則相衝突。指針一行的成本可預算，內容查閱交給 agent 依需要主動讀取。

### Alternative C：不建立治理級接線規則（Decision (c)），只修好這三份文件的接線，當作一次性勘誤

Rejected. 若不把「新規範文件須同 commit 接線」訂為規則，下一份新規範文件出現時會重演同樣的漏接——這正是 `docs/adr/adr-007-process-governance.md` 想終結的「同一種 drift 換個地方發生」。訂為治理級規則的成本很低（review checklist 多一項），但關閉的是整類問題而非單一實例。

### Alternative D：把接線規則寫進 `docs/verification-matrix.md`，不另開 ADR 決策段

Rejected. 矩陣本身聲明「本檔僅描述現況、不創設新規則」（見該檔開頭治理聲明），接線規則是一條新的治理規則（影響未來所有規範文件的交付定義），依 `docs/adr/adr-007-process-governance.md` Implementation Rules 1，新規則只能透過 Accepted ADR 提出；矩陣之後可以登記「本規則由 ADR-010 治理、無機械化防線」一行，但規則本體必須先在 ADR 內。

---

## Implementation Rules

1. `CLAUDE.md`「Workflow Orchestration」章下必須存在指向 `docs/orchestration.md`、`docs/verification-matrix.md`、`tasks/_templates/checkpoint.md`、`AGENTS.md` 四者的指針小節，且不得複寫其內容細節（僅檔名 + 一句話用途）。
2. `.claude/hooks/session-init.sh` 的 must-read echo 區塊必須包含指向 `docs/orchestration.md` 與 `docs/verification-matrix.md` 的一行；任何修改此行文字的 commit 必須同 commit 更新 `scripts/hook-smoke.sh` 對應斷言（比照 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` Implementation Rules 4 的既有要求）。
3. 未來任何新增的規範級文件（會被當作規則遵守，非執行紀錄或 backlog），必須在同一個 commit 內接上至少一個自動載入面（`CLAUDE.md` 指針 / `session-init.sh` must-read / `AGENTS.md` 指針）；review 時須檢查新增的 `docs/*.md` 或 `tasks/_templates/*.md` 規範性文件是否符合本條。
4. 本 ADR 不改變 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 既有的注入上限（8 條 lessons）與 session_id marker 去重邏輯；任何提案修改該邏輯須依 ADR-008 Implementation Rules 6 另開新 ADR，不得借本 ADR 之名夾帶。
5. 任何提案修改 1–4，必須先開新 ADR。
