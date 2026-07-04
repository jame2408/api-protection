# AGENTS.md

> 薄入口，給非 Claude Code harness 的 AI agent（或人類）快速定位規則位置。**本檔不複寫任何規則內容**，只放指針。規則正典見下方連結。

## 規則正典

所有工程規範（架構、測試策略、錯誤處理、命名、BDD 流程）的正典是 **`CLAUDE.md`**（repo root）。開始任何寫程式任務前，先讀它。

## 第一步

Clone 後、動手改任何程式碼前，先跑一次：

```bash
scripts/install-git-hooks.sh
```

這會安裝 pre-commit / pre-push hook，讓本機的 commit / push 檢驗與 CI 一致（見 `scripts/ci-checks.sh`）。

## 協調規則

多任務 / 多模型協調（任務怎麼分級、executor 的義務、何時該停下、交接格式）見 **`docs/orchestration.md`**。修改該文件的任一章節，須先開新 ADR（見 `docs/adr/adr-007-process-governance.md`）。

## 此 harness 拿不到的防線

Claude Code 專屬的第 1 層防線（寫的當下即時攔截）在其他 harness 下不會生效：

- **PreToolUse 攔截**（`.claude/hooks/pre-tool-edit.py`）— 編輯檔案當下即時擋下已知違規 pattern（如 `new Failure(`、bare-string code）。
- **session-init must-read 注入**（`.claude/hooks/session-init.sh`）— session 開始時自動注入必讀規則，確保 agent 開工前已看過規範。

**對策**：非 Claude Code harness 的 agent，動手寫 `backend/` 程式碼前，必須**主動**讀取：

1. `.claude/references/**/*.rule.md`（依語言 / 主題分類的細則）
2. 所有 `docs/adr/adr-*.md` 中狀態為 `Accepted` 的 ADR

這兩類文件是第 1 層防線失效後的唯一補償手段；commit 前與 push 前的機械化 gate（`scripts/ci-checks.sh` fast / full）不受 harness 影響，會照樣擋下違規，但那已經是第 2、3 層，比在寫的當下就避免要昂貴。
