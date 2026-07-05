# Phase E 任務規格 — 規範文件可發現性接線（ADR-010）+ 勘誤（executor 級：Sonnet）

> 背景：使用者指出兩個問題並已裁決方向：(1) `docs/orchestration.md`、`docs/verification-matrix.md`、`tasks/_templates/checkpoint.md` 沒有接上任何自動載入面，一般 session 的 agent 不知道它們存在；(2) 矩陣與 plan 宣稱「repo 無 `.editorconfig`」是錯的 — `backend/.editorconfig` 存在（僅含 2 檔 whitespace 豁免）。另有兩件記錄更正。可重派指令包。

## 角色與義務

- 誠實申報任何不確定到最終報告「Blockers / 不確定」節；不要腦補。
- **禁止 commit / amend 以外的 git 操作**；步驟 0 的 amend 與最後的 commit 除外。
- 不要動 `backend/`（除本規格明列項目 — 本任務不含任何 backend 變更）。

## 先讀（依序）

1. `docs/adr/adr-007-process-governance.md`、`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`（你的 ADR-010 要與它們銜接：CLAUDE.md 修改須源自 ADR、session-init 注入內容受 ADR-008 管轄 — ADR-010 是合法修改通道）
2. `docs/adr/_template.md` + `scripts/adr-lint.sh`
3. `.claude/hooks/session-init.sh` + `scripts/hook-smoke.sh`（你要改注入文字並同步 smoke test）
4. `CLAUDE.md`（找適合插入指針小節的位置 — 建議「Workflow Orchestration」章之下）

## 交付物

### 0. Amend HEAD commit message（僅限 HEAD 仍是 `416f12c` housekeeping commit 時做；否則跳過並回報）

GEMINI.md 刪除歸因已釐清：**使用者本人刪除（確認為無用內容）**。將 message 中「工作區被不明來源刪除…無法歸因」段更正為「使用者自行刪除（無用內容），orchestrator 查證後正式提交」。其餘內容保留。分支未 push，amend 安全。

### 1. `docs/adr/adr-010-norm-doc-discovery-wiring.md`

Decision 核心（使用者已裁決方向，你負責成文）：
- (a) **CLAUDE.md 新增「Orchestration & Verification」指針小節**（3–5 行，只放指針不複寫內容）：`docs/orchestration.md`（多模型協調憲章）、`docs/verification-matrix.md`（驗證登記表）、`tasks/_templates/checkpoint.md`（交接模板）、`AGENTS.md`（非 Claude harness 入口）。此修改源自本 ADR（滿足 ADR-007 治理）且為使用者發起（滿足 CLAUDE.md「changes initiated by the user」）。
- (b) **`session-init.sh` must-read 段追加一行**：「多模型協調與驗證機制：`docs/orchestration.md`、`docs/verification-matrix.md`」。此為 ADR-008 管轄內容的修改，本 ADR 即為其要求的新 ADR 通道 — 在 ADR-010 內明文銜接。
- (c) **接線規則（治理級）**：未來任何新增的規範級文件（會被當規則遵守的），必須在同一 commit 內接上至少一個自動載入面（CLAUDE.md 指針 / session-init must-read / AGENTS.md），否則視為未完成 —「寫了文件沒人知道」本身就是開環。
- 不在範圍：不改變 orchestration.md / 矩陣本身的內容；不動注入上限與 dedup 邏輯（ADR-008 規則不變）。

### 2. 實作 (a)(b) + smoke test 同步

- `scripts/hook-smoke.sh` 斷言 (a) 追加：首次注入輸出必須含 `docs/orchestration.md` 字樣（依 ADR-008 規則 4：改注入邏輯須同 commit 更新冒煙測試）。

### 3. 勘誤（`.editorconfig` 誤報）

- `docs/verification-matrix.md`：主表第 11 行、無防線區塊「命名慣例」行、審校紀錄第 2 點 — 三處「repo 未建/無 `.editorconfig`」更正為「`backend/.editorconfig` 存在，僅含 2 檔 `generated_code` whitespace 豁免；style/naming 規則未定義，權威來源仍為工具預設」。裁決狀態維持「待規格擁有者決定」不變。
- `tasks/process-improvement-plan.md` §8.3「`dotnet format` 權威來源模糊」條目：同樣更正描述。

### 4. `tasks/lessons.md` 追加兩條（放檔尾）

```
### [correction] Orchestrator 越位執行細節 — 路由表也約束 orchestrator 自己
**Date:** 2026-07-04
**Context:** 使用者糾正：zh-lint 實作、檔案修正、commit 操作等細節工作由 orchestrator（大型模型）親自執行，違反 docs/orchestration.md §1 自己訂的路由表（實作屬中型模型）。「規劃者不下場」不只是成本原則，也是憲章可移轉性的驗證 — orchestrator 自己繞過路由表，等於憲章沒有被完整遵守。
**Rule:** orchestrator 只做：設計裁決、ADR 起草或規格撰寫、review、與使用者的決策互動。任何有明確規格可循的實作（腳本、文件編輯、git 操作、勘誤）一律派 executor，即使「自己做比較快」。
**落地:** 本條 lesson + Phase E 起全部實作改派 executor（本任務即範例）。

### [correction] 「不存在」的斷言也要機械化驗證 — 矩陣誤報 .editorconfig 不存在
**Date:** 2026-07-04
**Context:** 驗證矩陣與 plan 宣稱「repo 無 .editorconfig」，實際 backend/.editorconfig 存在（executor 只查 repo root，orchestrator 抽驗也未抓到）。「存在性」核對清單只驗證了「列出的檔案存在」，沒驗證「宣稱不存在的東西真的不存在」。
**Rule:** 寫「X 不存在」的結論前，必須用遞迴搜尋驗證（如 find . -name 'X' 或 git ls-files '**/X'），不能只看單一目錄。
**落地:** 矩陣與 plan 勘誤（本 commit）；本條 lesson。
```

### 5. 進度回寫

`tasks/process-improvement-plan.md` §8.2 增列 Phase E 行（ADR-010 + 勘誤，狀態依實況）；§8.5「下一步」第 1 項前插入已完成註記或依實況調整。§9.3 不動。

## 驗收（全部輸出貼報告）

- `bash scripts/adr-lint.sh` 綠（10 檔）+ 故意紅驗證（暫刪 adr-010 governance clause → 紅 → 還原 → 綠，兩次輸出）。
- `bash scripts/hook-smoke.sh` 綠 + 故意紅驗證（暫時移除 session-init 新增行 → 紅 → 還原 → 綠）。
- `bash scripts/zh-lint.sh` 綠。
- 手動注入模擬輸出（`echo '{"session_id":"phase-e-manual"}' | SESSION_INIT_MARKER=$(mktemp) bash .claude/hooks/session-init.sh | head -15`）。
- 完成後 **執行一個 commit**（本任務例外允許）：訊息開頭 `governance(adr-010): 規範文件可發現性接線 + 勘誤`，內文列交付要點。commit 前確認 `git status` 只含本任務檔案。
- 最終報告：交付清單 + 驗證輸出 + Blockers/不確定。
