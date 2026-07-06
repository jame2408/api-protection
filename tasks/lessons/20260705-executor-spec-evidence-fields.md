---
date: 2026-07-05
type: correction
status: active
---

# Executor 派工規格必須內建取證指令與 friction 欄位 — 回報品質是 spec 精度問題

**Context:** executor 為滿足「scenario 名稱 + Passed 原文」的回報要求，自行摸索跑了 3 次 test suite（其中一次 `grep "Failed"` 誤中 MSBuild 雜訊行而整次無效）；另有 4 條 blocker 以下的不順（繞路、重跑）靠 orchestrator 事後追問才浮現。
**Rule:** 派工一律用 `tasks/_templates/executor-spec.md`；本條實質內容由其步驟取證原則與「非 blocker 的不順與繞路」必填欄承載。
**落地:** `tasks/_templates/executor-spec.md`（本 commit）。
