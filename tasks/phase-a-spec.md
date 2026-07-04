# Phase A 任務規格 — 協調憲章（executor 級：Sonnet）

> 來源：`tasks/process-improvement-plan.md` §9.4 Phase A（2026-07-04 使用者核准）。
> 本檔是可重派的指令包：session 中斷後，任何 orchestrator 可原樣重派給新 executor。

## 角色與義務

- 你是 executor。**誠實申報**任何不確定、卡住、規格模糊之處 — 寫進最終報告的「Blockers / 不確定」節。不要腦補繞過；停下來回報比錯誤產出便宜十倍。
- **禁止 commit**。產出檔案 + 跑驗證即可；orchestrator review 後才 commit。
- 不要動 `backend/`；不要動 `CLAUDE.md` 與 `tasks/todo.md`（工作區有既有未提交改動，非你的任務範圍）。

## 先讀（依序；只讀列出的，控制 token）

1. `tasks/process-improvement-plan.md` §8.3、§8.5、§9（全）
2. `docs/adr/_template.md` + `docs/adr/adr-004-failure-shape-and-claude-md-alignment.md`（格式與風格的唯一參考）
3. `CLAUDE.md` 全文 — 憲章必須與它一致（特別是 Working Agreement、Workflow Orchestration、ADR 章節）
4. `scripts/adr-lint.sh`（你的 ADR 要過它）

## 交付物

1. **`docs/adr/adr-007-process-governance.md`** — 把散在 process-improvement-plan 的治理規則正式化：
   (a) 規範修改唯一通道 = ADR；(b) 規範與衍生物同 commit 同步；(c) lessons 三類分類 + 必填「落地」欄位；(d) `docs/orchestration.md` 納入 ADR 管轄（修改憲章須先開 ADR）。
   同步項目：預期為「無需修改 CLAUDE.md」（它已陳述這些規則，ADR 只是正式化）— 若你發現實質矛盾：**停**，寫進 Blockers，不要自行改 CLAUDE.md。
2. **`docs/orchestration.md`** — 協調憲章，harness 中立用語，內容：
   - **模型分級路由表**：任務類型 → 執行者（腳本優先 / 小型模型：大量讀取、掃 repo、摘要、read-back / 中型模型：實作、review、修 bug / 大型模型：架構決策、規格裁決）。明文兩條：(i) 驗證優先機械化（腳本/測試/lint），AI review 只補機械化做不到的部分；(ii) 例行執行與 AI review 不依賴「短期顧問級」模型 — 協調者角色必須可由常設大型模型依本文件接手。
   - **Executor contract**：進度檔與實作同 commit、Green before commit、誠實申報 blocker、結束必產出 checkpoint（用模板）。
   - **全域停止條件**：同一測試/檢驗連紅 3 次 → 停 + 寫 blocker；需求或規格模糊 → 停 + 問；發現任務超出規格邊界 → 停；context 將盡 → 先寫 checkpoint 再停。
   - **Checkpoint schema**：指向 `tasks/_templates/checkpoint.md`（不複寫欄位）。
   - **Token 節約原則**：注入有上限、細節單一來源其餘放指針、續接靠 checkpoint 不靠重讀全史、大範圍掃描派小型模型。
3. **`tasks/_templates/checkpoint.md`** — 交接模板，以 §8.5 為範本抽象化。欄位：分支 / 已完成（含 commit hash）/ 待驗證 / 待裁決 / 下一步（每項獨立可中斷）/ 工作區狀態警告 / 如何接上。
4. **`AGENTS.md`**（repo root）— 非 Claude harness 的薄入口：規則正典在 `CLAUDE.md`；第一步跑 `scripts/install-git-hooks.sh`；協調規則見 `docs/orchestration.md`；明列「此 harness 拿不到的防線第 1 層（PreToolUse 攔截、session-init must-read 注入）→ 對策：動手寫 backend 前主動讀 `.claude/references/**/*.rule.md` 與 Accepted ADR」。**不得複寫任何規則內容，只放指針。**
5. **`tasks/process-improvement-plan.md` 進度回寫**：§8.2 manifest 增列 Phase A 行（狀態標「待 review」）；§8.3 的 governance ADR 項標記由 ADR-007 關閉；§9.4 Phase A 標記進度。§9.3 是裁決紀錄，勿動。

## 驗收（全部要做，輸出貼進報告）

- `bash scripts/adr-lint.sh` 全綠；**故意紅驗證**：暫時刪掉 adr-007 的 governance clause 再跑一次確認會紅，然後還原（兩次輸出都貼報告）。
- `bash scripts/ci-checks.sh fast` 綠。
- 單一真相來源自查：同一條規則不得完整出現在兩個檔案（指針除外）。
- 最終報告：交付清單（exact paths）+ 驗證輸出 + Blockers/不確定（可為空，但要說明「無」的信心依據）。
