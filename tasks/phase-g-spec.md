# Phase G 任務規格 — GitHub 上線與 CI 首跑（executor 級：Sonnet）

> 使用者提供 remote：`https://github.com/jame2408/api-protection.git`。目標：push、開 PR 觸發 `ci.yml` 首跑、綠後設 main required status check。關閉 §8.5「卡 GitHub」項。可重派指令包。

## 角色與義務

- 誠實申報；每個停止條件觸發時**立即停**並回報，不要繞過。
- 本任務不修改任何 repo 檔案內容（除非步驟 5 進度回寫）；允許 git remote / push / gh 操作與最後一個進度回寫 commit。

## 停止條件（先讀）

1. **repo 可見性為 public 時停**：`gh repo view jame2408/api-protection --json visibility` — 若 public，**不要 push**，回報並等裁決（todo #9：`appsettings.Development.json` 含明文 dev DB 密碼，push 到 public repo 前必須先處理）。private 則續行並在報告註明 #9 仍待處理。
2. push 被拒（權限 / 保護規則）→ 停，貼完整錯誤。
3. CI 首跑紅 → 停在該步，貼 job log 關鍵段（`gh run view --log-failed`），不要自行改 workflow 或程式碼修復。

## 步驟

1. `git remote add origin https://github.com/jame2408/api-protection.git`（若已有 origin：停，回報現值）。
2. 可見性檢查（停止條件 1）。
3. Push：先 `git push -u origin main`，再 `git push -u origin hardening/architecture-tests-mvp`。注意 pre-push hook 會跑 `ci-checks.sh full`（build+test，需 Docker，約數分鐘）— 這是預期行為，不要用 `--no-verify` 繞過。
4. 開 PR：`hardening/architecture-tests-mvp` → `main`。標題 `Hardening: architecture tests + governance loop (ADR-004~010)`；內文用 3–6 行摘要（架構測試 13 條、四層 gate、ADR-007~010、協調憲章與驗證矩陣），連結 `tasks/process-improvement-plan.md` §8。**PR 內文照 repo 慣例不加任何 AI 署名。**
5. 監看 CI：`gh pr checks <PR#> --watch`（或輪詢 `gh run list`）。綠 → 續行；紅 → 停止條件 3。
6. 設 required status check：`gh api -X PUT repos/jame2408/api-protection/branches/main/protection` 設定 `build-test` 為 required（strict 可 false；不啟用其他保護項，最小變更）。若權限不足：停，把可直接執行的完整指令與 GitHub UI 路徑寫進報告供使用者手動操作。
7. 進度回寫 + commit（訊息開頭 `docs(plan):`）：`tasks/process-improvement-plan.md` §8.3「CI 休眠」與 §8.5「卡 GitHub」項標記實況（首跑結果、PR 編號、protection 是否設妥）。回寫後這個 commit 也要 push 到分支。

## 最終報告

remote 現值、可見性、push 結果、PR URL、CI 首跑結果（連結 + 結論）、protection 設定結果、#9 狀態提醒、Blockers/不確定。
