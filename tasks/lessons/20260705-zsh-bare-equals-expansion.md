---
date: 2026-07-05
type: info
status: archived
---

# zsh 對裸 `=` 開頭的字樣做等號展開 — 分隔字串必須加引號

**Context:** ADR-018 首次 failure triage 即抓到最大 REPEAT 群組（4 筆同簽名 `(eval):N: == not found`，另有 `=== not found` 變體）：agent 在 Bash 工具慣用 `echo ===` 當輸出分隔，zsh 對裸 `=word` 參數做等號展開（解析為「尋找名為 `==` 的指令路徑」），直接報錯使整串複合指令中斷、該次工具呼叫作廢重跑。
**Rule:** 本機 shell 是 zsh：任何以 `=` 開頭的裸參數（含 `echo ===` 這類分隔字串）一律加引號（`echo '==='`），或改用不以 `=` 開頭的分隔符。
**落地:** `.claude/hooks/pre-tool-bash.py` `_ZSH_EQUALS_TOKEN` 段（矩陣 23a，commit `275e6ec`）；首例由 `scripts/failure-triage.sh` REPEAT 訊號捕獲。
