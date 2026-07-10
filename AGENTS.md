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

## Harness 第一層防線

Claude Code（`.claude/settings.json`）與 Codex（`.codex/hooks.json`）共用 `scripts/agent/hook.py`：session must-read／active lessons 注入、PreToolUse edit／Bash guard、post-edit syntax validation 與 failure observation 都只維護一份實作（見 `docs/adr/adr-023-cross-harness-hook-and-skill-parity.md`）。Codex 首次使用或 hook definition 變更後，先在 `/hooks` 檢視並信任本 repo hooks；不得把 `--dangerously-bypass-hook-trust` 當日常操作。

無論 harness 是否支援上述 lifecycle hooks，動手寫 `backend/` 程式碼前仍必須**主動**讀取：

1. `.claude/references/**/*.rule.md`（依語言 / 主題分類的細則）
2. 所有 `docs/adr/adr-*.md` 中狀態為 `Accepted` 的 ADR

Codex hook 只涵蓋其原生可攔截的 shell／`apply_patch`／MCP tool path；其他 harness 也可能完全沒有 hook。完整 enforcement boundary 仍是 commit 前與 push 前的 `scripts/ci-checks.sh` fast / full，不得因第一層已啟用而省略。
