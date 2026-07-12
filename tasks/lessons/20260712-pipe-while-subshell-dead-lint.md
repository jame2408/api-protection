---
date: 2026-07-12
type: info
status: active
---
# 管線右側的 while 是 subshell — gate 腳本在裡面累加違規計數會變死防線

**Context:** `scripts/adr-lint.sh` 檢查 6／7 以 `awk ... | while ... report` 累加 `violations`，管線右側的 while 在 subshell 執行，計數增量在迴圈結束後丟失——違規會印出紅字，但腳本仍 exit 0（合成探針證實：僅違反檢查 6 的檔案回報 "passed"）。死防線比沒有防線更糟：它提供「已檢查」的錯誤安心感。

**Rule:** gate／lint 腳本內，任何需要把狀態（計數器、旗標）帶出迴圈的 while，不得放在管線右側；一律用 process substitution（`while ...; do ...; done < <(cmd)`）。新 gate 上線的「故意紅」驗證必須斷言 **exit code**，不得只目視紅字輸出——本缺陷正是只驗輸出、未驗 exit code 才存活至今。

**落地:** `scripts/adr-lint.sh` 檢查 6／7 改 process substitution（ADR-028 同 commit，`43b2e45`），探針復跑 exit 1。
