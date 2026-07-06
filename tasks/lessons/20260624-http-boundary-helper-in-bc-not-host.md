---
date: 2026-06-24
type: decision
status: archived
---

# HTTP boundary helper 放 BC 內，不放 Host（BC→Host 會循環引用）

**Context:** Phase 3 對齊 RFC 9457 時，原計畫把 `ApiProblem` error-mapping helper 放 `Host/Http/`。但 endpoint（`CreateApiKeyEndpoint`）住在 KeyLifecycle BC，BC 呼叫 Host 會造成循環引用（Host 已 reference 各 BC）→ 編譯不過。SharedKernel 又是純 domain、不該注入 ASP.NET 型別。
**Rule:** HTTP boundary helper（回 `IResult`、用 `HttpContext`）放在「擁有該 endpoint 的 BC」內（如 `KeyLifecycle/Http/`，該 BC 已有 `FrameworkReference Microsoft.AspNetCore.App`）。等第二個 BC 也需要時再抽共用 web library，別預先放 Host 或污染 SharedKernel。dependency 方向：Host → BC → SharedKernel，不可逆。
**落地:** 防線＝編譯器（Host 已 reference 各 BC，BC→Host 必循環引用、build 失敗，違規當下即編譯不過，非事後檢驗）；placement 先例已成程式碼事實 `backend/src/KeyLifecycle/Http/ApiProblem.cs`（單一 error envelope 來源）。
