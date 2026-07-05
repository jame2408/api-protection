# Phase C 任務規格 — 驗證矩陣（executor 級：Sonnet）

> 目的：關閉 `tasks/process-improvement-plan.md` §9.2 O-6 —— 給全專案的驗證機制一張登記表：每條規則由什麼機制、在什麼時機、由誰（腳本／哪一級模型／人）驗證。可重派指令包。

## 角色與義務

- 誠實申報任何不確定之處到最終報告「Blockers / 不確定」節；發現「規則存在但查無對應機制」時**照實列入表中標 ⚠️ 無防線**，不要假裝有。
- **禁止 commit**。orchestrator review 後才 commit。
- 只新增 `docs/verification-matrix.md` 一個檔案。**不要動**其他任何檔案（`tasks/process-improvement-plan.md`、`tasks/lessons.md` 的回寫由 orchestrator 做；另一 executor 正在並行修改 `.claude/hooks/` 與 `scripts/`）。

## 先讀（依序）

1. `tasks/process-improvement-plan.md` §8.4（防線層次）、§9（協調層盤點）
2. `docs/orchestration.md` §1（模型分級路由表 — 「執行者」欄的分級用語以此為準）
3. `scripts/ci-checks.sh`、`scripts/source-lint.sh`、`scripts/adr-lint.sh`、`.claude/hooks/pre-tool-edit.py`
4. `backend/tests/Architecture.Tests/`（逐檔列出測試類與其鎖定的規則）
5. `docs/adr/adr-007-process-governance.md`、`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`
6. `CLAUDE.md`「Verification Standards」「Non-Negotiable Constraints」段（規則清單的主要來源）

## 交付物：`docs/verification-matrix.md`

- **文首治理聲明**：本檔為**登記表（descriptive）**，規範本體在指向的 ADR / rule 檔 / CLAUDE.md；不得複寫規則內容，只放指針（ADR-007 規則 5 精神）。任何檢驗機制新增／修改時，本表須同 commit 同步（ADR-007 規則 2）。
- **主表欄位**：`規則（一句話 + 權威來源指針）` | `機制（測試/lint/hook 的確切檔名）` | `時機層（寫的當下 / commit 前 / push 前 / CI / review 時）` | `執行者（腳本 / 小型模型 / 中型模型 / 大型模型 / 人）`。
- **涵蓋範圍**（至少）：
  - 13 條 Architecture.Tests（逐測試類列）
  - source-lint 的每個 pattern、adr-lint（結構性）、`dotnet format`
  - pre-tool-edit.py 的 4 個攔截 pattern（標注：僅 Claude Code harness 有效，其他 harness 見 `AGENTS.md` 對策）
  - hook-smoke.sh（ADR-008 新增，與本表同批落地 — 依 ADR-008 後狀態寫，不要寫 pending-lessons 舊機制）
  - BDD FunctionalTests（wire contract 鎖定）、SharedKernel.Tests
  - AI review 類：code-review skill（執行者：中型模型）、orchestrator review of executor 產出（執行者：大型模型；含簡體字掃描 — 見 `tasks/lessons.md` 2026-07-04 [correction]）
  - 人工類：ADR review checklist（CLAUDE.md ADR 段，執行者：人）
- **無防線區塊**：表尾列出「規則存在但無機械化檢驗」項（例：禁簡體、CancellationToken 傳播、unit coverage ≥80%、P99 效能門檻……以你實查為準），標 ⚠️ 並指向 §8.3 / todo 對應追蹤項；查無追蹤項者明說「未追蹤」。
- 不列 Tessl（§9.3 D-2 裁決：擱置，不入制度）。

## 驗收

- 矩陣中每一行的「機制」檔案路徑必須真實存在（自查：逐一 `ls` 或 grep 驗證，報告中確認）。
- 「執行者」欄用語與 `docs/orchestration.md` §1 一致（腳本／小型／中型／大型模型／人）。
- 最終報告：交付路徑 + 你發現的 ⚠️ 無防線清單摘要 + Blockers/不確定。
