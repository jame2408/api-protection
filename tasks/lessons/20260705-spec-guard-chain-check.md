---
date: 2026-07-05
type: correction
status: active
---

# 啟用後段 guard 場景的 spec 必須沿 guard 鏈核對請求形狀 — 佔位常值視同執行期值

**Context:** scenario「到期時間已過」spec 預測「接 seed 即綠」，但 When step 的佔位 scope `"any:read"` 從未註冊進 Scope Registry，guard 4b（scope 存在性）先於 guard 5（到期）把請求短路成 422，executor 正確停止回報 blocker、白跑一輪。orchestrator 核實了目標 guard 與 Then 映射，唯獨沒沿 handler guard 順序檢查該請求會不會被更早的 guard 攔下；8/44 故意紅的級聯形態（guard 4 破壞後落到 guard 5 的 422）其實已預先暴露同一盲點。
**Rule:** 派工一律用 `tasks/_templates/executor-spec.md`；本條實質內容由其背景欄「guard 鏈核對」註記承載（本 commit 補入）。
