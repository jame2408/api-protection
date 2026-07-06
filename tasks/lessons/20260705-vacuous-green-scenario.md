---
date: 2026-07-05
type: correction
status: active
---

# 啟用型 BDD 場景直接綠 — 測試「會失敗」的能力未被證明，必須補故意紅

**Context:** scenario「租戶狀態非 Active — 拒絕建立」的 slice 早已完整（guard／HTTP 映射／steps 全就位），移除 `@ignore` 後直接綠，整個週期沒有紅過 — vacuous pass 風險未被排除。使用者稽核後裁定補為義務。
**Rule:** 派工一律用 `tasks/_templates/executor-spec.md`；本條實質內容由其「故意紅（適用時必填）」欄承載。
**落地:** `tasks/_templates/executor-spec.md`「故意紅」欄（本 commit）。
