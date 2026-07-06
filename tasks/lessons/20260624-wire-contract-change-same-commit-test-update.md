---
date: 2026-06-24
type: decision
status: archived
---

# 改 wire contract 必須同 commit 更新斷言它的測試（否則套件紅）

**Context:** 把 error 從 `{error}` 改成 RFC 9457 時，發現 `CreateApiKeySteps.ThenCreateFailsWithReason` 原本把 body 反序列化成 `record ErrorResponse(string Error)` 斷言 `body.Error`。若只改 production 不改測試，既有通過場景立刻紅。
**Rule:** 變更 API wire contract（error 形狀、回應欄位）時，斷言該契約的測試必須同一個 commit 一起改 — 這是「契約變更」不是「test refactor」，不違反「production/test 不混改」（那條針對純 refactor）。順手把斷言升級成鎖完整 RFC 9457 shape，一改鎖住所有用該 step 的場景（含 @ignore 未上線的）。
**落地:** `CreateApiKeySteps.cs` `ThenCreateFailsWithReason` 改斷言 RFC 9457（type/title/status/errorCode/traceId + content-type）；故意紅驗證通過。
