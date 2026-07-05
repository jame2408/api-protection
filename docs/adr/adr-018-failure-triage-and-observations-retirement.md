# 學習迴圈回饋化：failures triage 義務與 observations 除役

> ADR-008 把兩個 jsonl 定位為「純 forensic、不注入」，結果是訊號只寫不讀 — 2026-07-05 loop-engineering 盤點（使用者裁決 Q1(a)）判定此為學習迴圈開環：「jsonl 都只是記錄，不是學習」。本 ADR 除役零消費者的 `observations.jsonl`，並把 `failures.jsonl` 接上「phase 收尾 triage → lesson／todo」的回饋通道。

---

## Status

Accepted (2026-07-05)

- Supersedes（部分）：`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 決策 §3 末項（「`observations.jsonl` 與 `failures.jsonl` 保留：純 append 的 forensic log」）與 Implementation Rule 5。該 ADR 其餘決策不受影響。
- 同步項目（同 commit）：`.claude/settings.json`（移除 PostToolUse observe 註冊）、`.claude/hooks/post-tool-observe.sh`（刪除）、`scripts/failure-triage.sh`（新增）、`docs/verification-matrix.md`（登記 triage 機制）、`tasks/checkpoint.md`（「如何接上」段加義務指針）、`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`（Status 段補部分被取代註記）。

---

## Context

### 現況

`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 決策 §3 末項：

```
observations.jsonl 與 failures.jsonl 保留：純 append 的 forensic log，
不注入、不佔 token，事後回溯有用；secret scrubbing 邏輯不動。
```

其 Implementation Rule 5 進一步規定「任何注入或 nag 機制不得讀取它們作為 context 來源」。實況（2026-07-05 實測）：

- `.claude/observations.jsonl`：3,134 筆、記錄每一次工具呼叫含完整檔案內容、無界成長、**零讀者** — 內容與 harness 自身的 transcript 完全重複。
- `.claude/failures.jsonl`：24 筆，**已含可學習的重複 pattern**（同一 session 內三筆同型 `(eval):1: == not found` — zsh 將 `echo ===` 後的裸 `==` 當作條件運算子），但自建檔以來沒有任何機制或義務讀過它。

### 不決定會發生什麼

四段迴圈（訊號 → 決策 → 落地 → 回寫）中，failures 通道的訊號段在收數據，其餘三段不存在：重複踩的坑要等到「人剛好記得」才會變成 lesson。observations 則是負資產 — 付儲存與隱私面成本，換一個「有觀測」的錯覺。「事後回溯有用」的假設兩個月內沒有發生過一次實際回溯。

---

## Decision

### 1. `observations.jsonl` 除役

`.claude/settings.json` 移除 PostToolUse 的無 matcher observe 註冊（保留 `post-edit-validate` 註冊不動）；刪除 `.claude/hooks/post-tool-observe.sh`；刪除本機 `.claude/observations.jsonl`（gitignored，無 git 歷史）。

```diff
   "PostToolUse": [
-    {
-      "hooks": [
-        { "type": "command",
-          "command": "bash \"$CLAUDE_PROJECT_DIR/.claude/hooks/post-tool-observe.sh\"" }
-      ]
-    },
     {
       "matcher": "Edit|Write",
       "hooks": [ ... post-edit-validate.sh ... ]
     }
   ],
```

### 2. 新增 `scripts/failure-triage.sh`（報表工具，非 gate）

讀取 `.claude/failures.jsonl`（可用第一參數覆寫路徑供 fixture 測試），依「tool × error 首行正規化簽名」分組計數、由多至少輸出；計數 ≥ 2 的簽名標記 `REPEAT`（= lessons 觸發條件「repeated issue」的機械前哨）。格式毀損的行靜默略過並計數申報；空檔案或檔案不存在時輸出說明並 exit 0。輸出形如：

```
[failure-triage] 24 records, 2026-07-01 .. 2026-07-05
  3x  REPEAT  Bash    (eval):1: == not found
  1x          Bash    Exit code 1 ...
```

### 3. phase 收尾 triage 義務

orchestrator 於 phase 收尾更新 `tasks/checkpoint.md` 之前，必須跑一次 `scripts/failure-triage.sh`；每個 `REPEAT` 簽名必須三選一處置，不得無聲略過：

- (a) 轉 lesson（依 `tasks/lessons.md` 既有觸發條件與格式）；
- (b) 開 `tasks/todo.md` 項目；
- (c) 於 checkpoint「待裁決」或「工作區狀態警告」欄記錄「不轉之理由」。

### 4. ADR-008 邊界修訂

「不得作為**注入／nag** 的 context 來源」維持不變（防 token 洩漏的原意保留）；phase 收尾的 deliberate triage 讀取自本 ADR 起為明文允許且必要。

### 不在本 ADR 範圍

- `post-tool-failure.sh` 的記錄行為與 secret scrubbing 邏輯不動。
- 不重建 pending-lessons 式自動 flagging——「機械 flag 零 harvest」的教訓（ADR-008 Context 第 3 點）不被推翻，lesson 判斷仍在人／orchestrator。
- triage 不接入 `scripts/ci-checks.sh`，不是 gate。

---

## Rationale

### 為何除役 observations 而不是加輪替或上限

零消費者的記錄輪替後仍是零消費者 — 成本問題（磁碟、隱私面）是次要，主要問題是它給「有觀測防線」的錯覺，正是 loop-engineering 所稱比沒有更糟的死機制。內容與 harness transcript 全量重複，回溯需求可由 transcript 滿足。未來若出現具體消費場景（例如工具行為統計），重建 hook 是一次 commit 的成本。

### 為何 triage 是義務而不是 gate

`failures.jsonl` 天然混雜刻意紅（TDD 確認未實作）與探索性失敗，機械上與真異常不可分——這正是 ADR-008 退役 pending-lessons 的實證根因，gate 化必誤紅並訓練人繞過。學習判斷不可機械化，但「看訊號的節奏」可以制度化：掛在「更新 checkpoint」這個既有硬義務（executor contract 第 4 條）之前，零新增儀式面。

### 為何簽名分組這麼粗糙就夠

目的不是錯誤分類學，是讓「第二次出現」自動浮出水面。分組只是人工判讀的排序輔助，誤併誤拆由判讀矯正，不值得投資更精細的正規化。

---

## Consequences

### Positive

- failures 通道四段接通：訊號（hook）→ 決策（phase 收尾 triage）→ 落地（lesson／todo）→ 回寫（checkpoint 記錄）。
- 假觀測除役，工具呼叫不再付雙寫成本；`.claude/` 本機狀態縮回「有消費者的檔案」。
- 重複踩坑的發現不再依賴人的記憶力，改由計數觸發。

### Negative / Trade-offs

- 除役後失去全量工具呼叫記錄。
  - Mitigation: harness transcript 本身即全量記錄；異常面由 `failures.jsonl` 持續覆蓋。
- triage 義務仍屬人工紀律，可能被跳過。
  - Mitigation: 掛點是 checkpoint 更新（不產出 checkpoint 即違 executor contract）；`docs/verification-matrix.md` 誠實登記為人工類，不假裝機械化；跳過而漏掉的 REPEAT 會在下次 triage 以更高計數再現，失效模式是延遲而非遺失。
- 簽名正規化可能把不同根因併組、或同根因拆散。
  - Mitigation: 報表供人判讀，分組錯誤由判讀矯正；正規化規則簡單（首行截斷），行為可預測。

---

## Alternatives Considered

### Alternative A：保留 observations，加輪替／大小上限

Rejected. 輪替只是把「無人讀的大檔案」變成「無人讀的小檔案」，開環不變；且保留即持續支付「有觀測」錯覺的制度成本。

### Alternative B：triage 接入 ci-checks 作為 gate（有 REPEAT 即紅）

Rejected. 刻意紅與真異常機械不可分（ADR-008 實證），必然誤紅；誤報的 gate 會訓練人繞過或關閉，傷害整條防線的可信度。

### Alternative C：REPEAT 簽名自動寫入 `tasks/lessons.md`

Rejected. lessons 是判斷型三通道（ADR-008 決策 §3），機械寫入重蹈 pending-lessons 158 條零 harvest 的覆轍；自動生成的 lesson 沒有 Context 與落地判斷，只是換個檔案堆噪音。

### Alternative D：維持現狀（純 forensic 定位）

Rejected. 2026-07-05 盤點的使用者裁決明確：無消費者的訊號是開環，「記錄不是學習」；「事後回溯有用」在兩個月內未發生過一次，不足以支撐保留兩條 write-only 管線。

---

## Implementation Rules

1. `.claude/settings.json` 的 PostToolUse 不得含 post-tool-observe 註冊；`.claude/hooks/post-tool-observe.sh` 不存在。**驗收**：

   ```bash
   grep -rn "post-tool-observe" .claude/settings.json .claude/hooks/ scripts/
   # 預期 0 命中（歷史 ADR 本文與 tasks/archive/ 為歷史紀錄，豁免不改）
   ```

2. `scripts/failure-triage.sh` 存在且 bash 3.2 相容（受 `scripts/source-lint.sh` bash_compat 段管轄）；對含 3 筆同簽名的 fixture 輸出 `REPEAT` 標記；對空檔案與不存在的檔案 exit 0。
3. phase 收尾更新 `tasks/checkpoint.md` 前必跑 `scripts/failure-triage.sh`；每個 `REPEAT` 簽名依決策 §3 三選一處置，不得無聲略過。
4. `docs/verification-matrix.md` 與本 ADR 同 commit 登記 triage 機制（人工類，時機層 = phase 收尾）。
5. `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` Status 段同 commit 補註：決策 §3 末項與 Implementation Rule 5 已由本 ADR 部分修訂。
6. 任何提案修改 1–5，必須先開新 ADR。
