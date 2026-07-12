---
date: 2026-07-10
type: correction
status: active
---
# zsh 的 status 是唯讀變數，故意紅清理不可依賴未初始化 mode

**Context:** 驗證 machinery 故意紅時以 `status=$?` 記錄 exit code，zsh 立刻中止；後續 restore trap 又因 mode 變數未可靠初始化而沒有恢復 executable bit。
**Rule:** zsh 指令不得賦值給唯讀 `status`，一律用 `exit_code`；會暫改檔案 mode 的驗證必須在註冊 trap 前初始化並驗證 restore 值，或使用已知固定 mode，且結束後立即以正向 gate 複驗。
**落地:** Rule 1（`status=` 賦值）已於 2026-07-12 機械化——`scripts/agent/hook.py` `pre-tool-bash`（`_ZSH_STATUS_ASSIGN` regex，矩陣 23c），觸發依據：本 lesson 注入下仍復發（2026-07-10 兩起並立、2026-07-12 failure-triage REPEAT ×2）＝習慣承載失效。Rule 2（mode 暫改的 restore trap 紀律）仍靠習慣承載，維持 `status: active`；本次已以 `chmod 755` 恢復 `scripts/agent/hook.py` 並重跑 `scripts/machinery-check.sh` 綠（原始事故記錄）。
