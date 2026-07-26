---
date: 2026-07-05
type: correction
status: active
---

# Executor 派工規格必須內建取證指令與 friction 欄位 — 回報品質是 spec 精度問題

**Context:** executor 為滿足「scenario 名稱 + Passed 原文」的回報要求，自行摸索跑了 3 次 test suite（其中一次 `grep "Failed"` 誤中 MSBuild 雜訊行而整次無效）；另有 4 條 blocker 以下的不順（繞路、重跑）靠 orchestrator 事後追問才浮現。
**Context 追加（2026-07-26，跨模型證據）:** 同一失誤家族——**協調者把「關於 repo 現狀的宣稱」寫進 spec 而未先機械化取證**——在兩個不同協調者模型下各重現兩次：Fable 5（2026-07-12）誤列 caller 數 5 實為 7、誤把既有 step 列為新增；Opus 5（2026-07-26）誤稱同步落點「唯一」實為 2 處、未交代多個 Given 疊加時後續 Given 如何與前一個組合（致 executor 選了重新 seed，會讓下一條場景在錯誤前提下變綠）。**結論：屬結構性缺陷，不是特定模型的能力差異**；對策一律走模板機械化，不得寄望協調者的習慣或模型更替。
**Rule:** 派工一律用 `tasks/_templates/executor-spec.md`；本條實質內容由其背景欄取證條文、步驟取證原則與「非 blocker 的不順與繞路」必填欄承載。**新增失誤家族時，條文一律加進模板（跨 session 常駐），不得只寫在 `tasks/checkpoint.md`** ——後者的「下一步」區塊會隨 phase 收尾改寫，寫在那裡的條文會靜默蒸發。
**落地:** `tasks/_templates/executor-spec.md`（2026-07-05 建檔；2026-07-12 補列舉取證與新 step 宣稱兩條；2026-07-26 補存在性數量斷言、多 Given 組合、取證指令自身可用性、mutation 選點四條）。
