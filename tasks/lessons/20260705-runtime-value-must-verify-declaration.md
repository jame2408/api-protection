---
date: 2026-07-05
type: correction
status: active
---

# Spec 背景欄的執行期值敘述必須讀宣告求證 — null 推測害 executor 白跑一輪

**Context:** 派工「Active 金鑰數達到上限」場景時，spec 背景欄寫「`_ctx.CurrentTenantId` 此場景中為 null」— 這是「沒有 Given 設過它」的推測，未讀 `FunctionalTestContext` 宣告（實為 `= string.Empty` 預設）。executor 依 spec 寫 `is null` 條件恆假，多跑一輪測試才自行改成 `string.IsNullOrEmpty` 修正。
**Rule:** 派工一律用 `tasks/_templates/executor-spec.md`；本條實質內容由其背景欄執行期值求證註記承載。
**落地:** tasks/_templates/executor-spec.md 背景欄求證註記（docs/adr/adr-019-token-economy-charter-rules.md）。
