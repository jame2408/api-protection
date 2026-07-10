---
date: 2026-07-10
type: correction
status: active
---
# zsh 的 status 是唯讀變數，故意紅清理不可依賴未初始化 mode

**Context:** 驗證 machinery 故意紅時以 `status=$?` 記錄 exit code，zsh 立刻中止；後續 restore trap 又因 mode 變數未可靠初始化而沒有恢復 executable bit。
**Rule:** zsh 指令不得賦值給唯讀 `status`，一律用 `exit_code`；會暫改檔案 mode 的驗證必須在註冊 trap 前初始化並驗證 restore 值，或使用已知固定 mode，且結束後立即以正向 gate 複驗。
**落地:** 本條 lesson；本次已以 `chmod 755` 恢復 `scripts/agent/hook.py` 並重跑 `scripts/machinery-check.sh` 綠。
