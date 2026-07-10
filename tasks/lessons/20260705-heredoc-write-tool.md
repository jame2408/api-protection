---
date: 2026-07-05
type: correction
status: active
---

# heredoc 寫檔在本 harness 不可靠 — 寫檔用 Write 工具；被自動轉背景的指令必須立即收尾

**Context:** 同一 session 內 heredoc 咬人兩次：(1) for 迴圈內 `python3 - <<EOF` 卡 stdin 致 2 分鐘 timeout；(2) `cat > file <<'EOF'` 寫檔被 harness 自動轉背景，檔案寫成但 `cat` 卡等 stdin 不終止，掛在「running」3.5 小時 — orchestrator 當下已注意到「怎麼自己轉背景了」卻只讀輸出檔就當完成，未追蹤；事後排查又因 `ps` grep 只列預期嫌犯（headless/dotnet/stryker）而漏掉 `cat`，靠使用者出示 UI 截圖才定位。
**Rule:** (1) 前景指令被 harness 自動轉背景 = 異常訊號，當下必須追蹤到終態（完成或 TaskStop），不得只驗證副作用（檔案存在）就放行。(2) 排查殘留程序不得只 grep 預期樣式，要以 session 起始時間列全量（如 `ps -o etime` 過濾長時程序）。
**落地:** 原「寫檔禁 heredoc、一律用 Write 工具」段已機械化 — `scripts/agent/hook.py` `pre-tool-bash` heredoc 攔截（矩陣 23；原始落地 commit `275e6ec`，跨 harness 遷移見 ADR-023），2026-07-10 triage 自 Rule 行移除改防線代記；餘上列兩段為行為紀律。多行 commit message 改以 Write 寫訊息檔＋`git commit -F <file>`，不用 heredoc。
