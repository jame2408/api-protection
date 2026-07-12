---
date: 2026-07-12
type: correction
status: active
---

# @ignore 佇列查找必須用行號數值排序 — 純字典序把同檔 5 行排在 19 行後，錯誤指標曾寫進帳面

**Context:** 場景 33/46 收尾時，orchestrator 以 CLAUDE.md 的 `grep -rn "@ignore" … | sort | head -1` 判定下一場景，純字典序把 `05_RotateKey.feature:5`（首場景「成功啟動金鑰輪替」）排在 `:19`（次場景）之後，錯誤的「下一個」指標經派工 spec 寫進 `tasks/bdd-progress.md`，並在 checkpoint 下一步欄衍生出「首場景已綠、RotateKey 切片既存」兩條錯誤推論。下一輪勘查讀 feature 全文（而非只信 grep 輸出）才現形，勘誤 commit `843b9f3`。`tasks/bdd-progress.md` 的「如何找到下一個場景」段其實早已內建正確指令與警語（`sort -t: -k1,1 -k2,2n`），但 CLAUDE.md 的簡化版仍是純 `sort`，兩處 drift。

**Rule:** (1) 查找下一個 `@ignore` 場景一律用佇列 SSOT `tasks/bdd-progress.md` 記載的指令（`grep -rn "@ignore" backend/tests/FunctionalTests/Features/ | sort -t: -k1,1 -k2,2n | head -1`），不得用純字典序 `sort`。(2) 對 grep 輸出的佇列判定，落帳前以 feature 檔原文覆核目標場景前面沒有更早的 `@ignore`——工具輸出的排序假設也是一種需要驗證的執行期值。

**落地:** 本條 lesson；CLAUDE.md 的簡化指令修訂屬使用者裁決（已於 2026-07-12 session 面呈）。
