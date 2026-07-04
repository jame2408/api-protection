# 學習迴圈機械層重設計：session 注入有界化、pending-lessons 管線退役

> `session-init.sh` 的 lessons 注入與 pending-lessons flagging 管線經實測已失效或零產出。本 ADR 決定：注入改為「每 session 一次、有上限」，flagging 管線退役，並為 hook 建立冒煙測試防止再次靜默死亡。

---

## Status

Accepted (2026-07-04)

同步項目：`.claude/hooks/session-init.sh`（重寫注入邏輯）、`.claude/hooks/post-tool-observe.sh` 與 `.claude/hooks/post-tool-failure.sh`（移除 pending-lessons 寫入）、`scripts/hook-smoke.sh`（新增）、`scripts/ci-checks.sh`（fast 模式接入冒煙測試）、`.gitignore`（新增 `.claude/*.marker`）、`.claude/pending-lessons.jsonl`（退役移除）、`tasks/process-improvement-plan.md` §8.2/§8.3/§9.4 回寫。預期無需修改 `CLAUDE.md`：其 Self-Improvement Loop 段只規定 lessons 觸發條件與 `tasks/lessons.md` 落點，從未提及 pending 管線或注入實作。

---

## Context

### 實證發現（2026-07-04，均已逐項驗證）

1. **lessons 注入靜默死亡**：`session-init.sh` 以 `awk '/^---$/{found++; next} found>=2{print}'` 抽取 lessons 內容，要求檔案內出現**兩個** `---` 分隔線；現行 `tasks/lessons.md` 只有一個（模板演化時結構改變）。結果：抽取內容永遠為空 → script 提前 `exit 0` → lessons 與 pending 計數兩段**從未注入**。當日 session 實測注入內容只有 must-read 段。
2. **首 turn 偵測失效，must-read 每 turn 重複注入**：hook 以 `json.load(transcript)` 計算 user turns，但 transcript 實為 JSONL（逐行 JSON），`json.load` 必然拋例外 → fallback 回傳 0 → `0 > 1` 恆假 → **每一個 user prompt 都重新注入 must-read**。同一 session 內實測第二 turn 再度注入。這是 token 的持續性洩漏，方向與 §9.2 O-5「注入應有界」正好相反。
3. **pending-lessons flagging 結構性噪音、零產出**：`pending-lessons.jsonl` 累積 158+ 條，其中約 120 條為 bash error/failure flag —— 但本專案方法論本身就大量製造「刻意紅」（TDD 確認未實作、探索性 grep），機械上無法與真問題區分；約 38 條 config 修改 flag 中多數指向 **repo 之外**的路徑（`~/.claude/skills/...`），因為 hook 未限定專案範圍。兩個月內從此管線 harvest 的 lesson 數：**0**。`tasks/lessons.md` 現有 7 條全部來自 session 內的人／orchestrator 判斷。

### 問題本質

三個失效都屬同一類：**hook 邏輯沒有任何檢驗**，違規（失效）不會讓任何東西變紅。lessons 注入死了半個月以上無人察覺；重複注入洩漏 token 無人察覺；flagging 噪音堆積無人消化。這是 loop-engineering 所稱「給人有防線錯覺的死機制，比沒有更糟」。

---

## Decision

### 1. 注入以 session marker 去重，不再猜測 transcript 格式

`session-init.sh` 改用 hook payload 的 `session_id` 與 marker 檔（`.claude/session-init.marker`，內容為上次注入的 session_id）判斷是否已注入：session_id 與 marker 相同 → 跳過；不同 → 注入並更新 marker。

```diff
- HUMAN_TURNS=$(... json.load(transcript) ...)   # transcript 是 JSONL，恆失敗 → 每 turn 注入
+ SESSION_ID=$(... payload["session_id"] ...)
+ [ "$SESSION_ID" = "$(cat .claude/session-init.marker)" ] && exit 0   # 每 session 僅一次
```

理由：transcript 格式由外部（harness）擁有，任何格式假設都可能再次靜默失效；marker 是自有狀態，行為完全可測。

### 2. 注入內容有界：must-read + 最近 8 條 lessons + 計數指針

- lessons 抽取改以 `### [` 條目錨點切塊（結構穩定，不依賴 `---` 分隔線數量），只注入**最近 8 條**（檔案由上到下為新到舊時取前 8 塊；反之取後 8 塊，以現行檔案實際順序為準）。
- 注入結尾加一行：`（完整 N 條見 tasks/lessons.md）`。
- 上限 8 為明文常數；調整須修改本 ADR 或開新 ADR。8 條之外的舊 lesson 不再注入 —— 依 ADR-007，每條 lesson 必有「落地」欄位指向機械化產物，已落地的 lesson 由防線代為記憶，不需要永久佔用注入預算。

### 3. pending-lessons 管線退役

- `post-tool-observe.sh`、`post-tool-failure.sh` 移除所有對 `pending-lessons.jsonl` 的寫入；`session-init.sh` 移除 pending 計數段（該段本來就不可達）。
- 既有 `.claude/pending-lessons.jsonl` 直接刪除（gitignored 本機檔，git 無歷史；其內容已於本 ADR Context 完成最終 triage：120 條 bash 噪音、38 條多為 repo 外 config，0 條值得轉 lesson）。
- lessons 捕捉機制收斂為判斷型三通道：CLAUDE.md Self-Improvement Loop 觸發條件（session 內自捕）、`/lesson` skill（使用者觸發）、orchestrator review（executor contract 的誠實申報 + 覆核）。
- `observations.jsonl` 與 `failures.jsonl` **保留**：純 append 的 forensic log，不注入、不佔 token，事後回溯有用；secret scrubbing 邏輯不動。

### 4. hook 冒煙測試接入 ci-checks fast

新增 `scripts/hook-smoke.sh`：以 fixture payload 實跑 `session-init.sh`，斷言 (a) 新 session_id → 輸出含「必讀規範」與 `tasks/lessons.md` 最新一條的標題；(b) 同 session_id 第二次呼叫 → 輸出為空；(c) 測試自建臨時 marker 環境，不污染真實 marker。接入 `scripts/ci-checks.sh` fast 模式 —— 注入邏輯再次靜默死亡時，commit 就會變紅。

### 不在本 ADR 範圍

- 不動 `pre-tool-edit.py`（PreToolUse 攔截，運作正常）。
- 不動 `observations.jsonl` / `failures.jsonl` 的記錄行為與 secret scrubbing。
- 不改 must-read 段的文字內容（只改注入頻率）。
- 不規範 `tasks/lessons.md` 的條目格式（ADR-007 已定）。

---

## Rationale

### 為何退役 flagging 而不是修好它（加 scope、去重）

兩個月零 harvest 不是 scope 問題，是結構問題：flag 的主要來源（bash error）在本專案方法論下**必然**充滿刻意紅，機械規則無法區分「TDD 確認紅」與「真異常」；而真正產出的 7 條 lessons 全部來自判斷（人或 orchestrator review）。修好一條沒有 consumer 的管線是浪費；若未來出現「機械可判別的 lesson 訊號」，屆時開新 ADR 重建，成本不高於現在保留殘骸。

### 為何注入上限選 8 條而不是全量或索引式

全量注入隨條目線性成長，正是 O-5 要關閉的洩漏。純索引（只給標題）則要求 agent 主動回讀，弱模型不可靠。8 條完整條目約 50 行，token 成本固定可預算，且「最近的 lessons 最可能對當前工作有效」—— 更舊的已由落地欄位轉為機械防線。

### 為何用 marker 檔而不是修正 transcript 解析（改 JSONL 逐行 parse）

即使今天把 JSONL 解析修對，transcript 格式仍由 harness 擁有，下次格式演化又是一次靜默失效，且 fallback 行為（0 turns）會把失效偽裝成「每 turn 注入」而非報錯。marker 檔是本專案自有狀態：語意單純（記住上次注入的 session）、失效模式良性（最壞多注入一次）、可被冒煙測試完整覆蓋。

---

## Consequences

### Positive

- 注入恢復實際生效（lessons 真的進 context），且每 session 只付一次 token 成本；must-read 每 turn 重複注入的洩漏關閉。
- 學習迴圈狀態誠實化：不再有「158 條 pending 等待 triage」的假待辦；lessons 捕捉責任明確落在判斷型通道上。
- hook 邏輯首次有紅綠燈：再次靜默死亡會在 commit 前被 `hook-smoke.sh` 攔下。

### Negative / Trade-offs

- 取消自動 flagging 後，「當下沒意識到的坑」可能漏捕。
  - Mitigation: executor contract 強制 checkpoint 與誠實申報（`docs/orchestration.md` §2）、orchestrator review 為第二層捕捉；`failures.jsonl` 保留 forensic 回溯能力。
- 第 8 條以外的舊 lessons 不再出現在 context，新 session 可能重踩很舊的坑。
  - Mitigation: ADR-007 要求每條 lesson 的「落地」欄位指向機械化產物；已落地者由防線代為記憶，未落地者本來就該補落地而非靠注入撐著。
- marker 檔是新的本機狀態，損壞或殘留會影響注入行為。
  - Mitigation: 失效模式良性（重複注入一次或跳過一次）；`hook-smoke.sh` 用臨時環境覆蓋兩個方向的行為；檔案進 `.gitignore` 不污染 repo。

---

## Alternatives Considered

### Alternative A：保留 flagging，加專案路徑 scope 與同因去重

Rejected. 零 harvest 的根因是「bash 刻意紅與真異常機械不可分」，scope 與去重都不解決它；修好之後仍然沒有 consumer。先有需求證據再重建。

### Alternative B：只修 awk 分隔線 bug，維持全量注入

Rejected. 恢復的是一個無界成長的注入 —— 條目到 30 條時每 session 注入數千 token，正是 §9.2 O-5 指出的洩漏方向。修 bug 不等於修設計。

### Alternative C：完全移除 lessons 注入，改由 agent 依 CLAUDE.md 自行讀取

Rejected. 依賴 agent 自律讀檔對弱模型不可靠（此點正是 session-init 存在的理由，見 §2 根因 4）；注入機制修好後成本低（每 session 一次、有上限），收益明確。

### Alternative D：改為逐行解析 JSONL transcript，保留「首 turn 偵測」設計

Rejected. transcript 格式由 harness 擁有，格式再演化就再壞一次，且失效是靜默的；marker 檔語意更簡單、完全可測。

---

## Implementation Rules

1. `session-init.sh` 的注入去重必須基於 hook payload `session_id` + `.claude/session-init.marker`；禁止以 transcript 內容或格式假設判斷 session 邊界。
2. 注入內容固定為：must-read 段 + `tasks/lessons.md` 最近 8 條條目（以 `### [` 錨點切塊）+ 總數指針一行；上限 8 的變更須先開新 ADR。
3. `.claude/hooks/` 內任何 hook 不得寫入 `pending-lessons.jsonl`；驗收：`grep -rn 'pending-lessons' .claude/hooks/ scripts/` 預期 0 命中。
4. `scripts/hook-smoke.sh` 必須在 `scripts/ci-checks.sh` fast 模式內執行；任何修改 `session-init.sh` 注入邏輯的 commit 必須同 commit 更新冒煙測試。
5. `observations.jsonl` 與 `failures.jsonl` 保留 append-only forensic 用途，任何注入或 nag 機制不得讀取它們作為 context 來源。
6. 任何提案修改 1–5，必須先開新 ADR。
