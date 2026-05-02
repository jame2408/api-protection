# 錯誤處理與跨 BC Contract 決策

> 本 ADR 記錄 Repository、Handler、HTTP boundary、跨 BC contract 之間的錯誤處理責任分工。

---

## Status

Accepted (2026-04-30)

---

## Context

專案目前同時存在兩個需要釐清的規範問題：

1. `.claude/references/dotnet/exceptions.rule.md` 的 Repository 範例使用 `Result<T, Failure>` 與 `try/catch` 包裝 DB exception，但實際 Repository implementation 回傳 raw type，例如 `Task`、`Task<int>`、`Task<bool>`。
2. `SharedKernel/Contracts/IConsumerValidator.cs` 回傳 `ConsumerValidationResult`，表面上和「Service 必須回傳 `Result<T, Failure>`」規則衝突。

如果不釐清，後續實作與 code review 容易出現兩種錯誤：

- 在 Repository 層把 DB exception 轉成 business `Failure`，導致 infrastructure failure 與 business failure 混在一起。
- 強迫跨 BC contract 暴露 `Result<T, Failure>`，讓 contract 綁定 consuming BC 的錯誤處理形狀。

---

## Decision

### 1. Repository 回傳 raw type

Repository 不回傳 `Result<T, Failure>`，也不 catch `DbException` 轉 business `Failure`。

Repository 的責任是資料存取：

- 查詢 entity、primitive、collection。
- 儲存 aggregate。
- 讓 DB / infrastructure exception bubble up。

DB / infrastructure exception 屬於 unexpected failure，統一由 HTTP boundary 的 `UnhandledExceptionMiddleware` 記錄並轉成 5xx response。

### 2. Handler 負責 business failure

Handler 仍然使用 `Result<T, Failure>` 表達可預期的 business failure，例如：

- tenant / consumer validation failed
- active key count exceeds limit
- key name duplicated
- scope not found
- command validation failed

Handler 不處理 Repository 的 DB exception。

### 3. 跨 BC contract 可以回傳 contract DTO

位於 `SharedKernel/Contracts/` 的跨 BC contract 可以回傳 contract-specific DTO，不必回傳 `Result<T, Failure>`。

目前範例是：

- `IConsumerValidator`
- `ConsumerValidationResult`
- `ConsumerValidationFailureCodes`

Consuming Handler 在 BC 邊界將 contract DTO 的 `ErrorCode` 轉成 `Failure`。

### 4. HTTP boundary 負責 response mapping

Endpoint / Controller 負責將 `Failure.Code` 對應成 HTTP status code。

未處理例外則由 `UnhandledExceptionMiddleware` 統一處理，避免 exception detail 外洩到 response。

---

## Rationale

### Repository 不包 Result

Repository raw return 保持責任單純：

- `null`、`false`、`0` 等查詢結果可以由 Handler 解讀成 business rule。
- DB exception 不代表 domain-level failure，沒有穩定 business code。
- 避免建立不存在或過早抽象的 `RepositoryFailureCodes`。

### 跨 BC contract 不強迫 Result

跨 BC contract 是 integration boundary，不是單一 BC 的 application service。

讓 contract 回傳 DTO 有幾個好處：

- contract 不綁定特定 BC 的 `Result<T, Failure>` shape。
- error code 維持穩定 contract。
- consuming BC 可以自行決定如何把 error code 轉成 `Failure` 與 HTTP response。

---

## Consequences

### Positive

- Repository、Handler、HTTP boundary 的責任更清楚。
- Code review 規則可以避免誤判 `IConsumerValidator`。
- AI coding agent 不會再依照過時範例產生 `Result<T, Failure>` repository。
- 未處理例外有統一 logging 與 generic 5xx response。

### Negative / Trade-offs

- Repository caller 不能只靠 `Result` 判斷所有錯誤；unexpected exception 會走 middleware。
  - Mitigation: `UnhandledExceptionMiddleware` 統一捕捉並轉 5xx，logging context 在邊界補齊；caller 不需也不該感知 DB exception 細節。
- 跨 BC contract DTO 需要維持 error code discipline，避免裸字串擴散。
  - Mitigation: 規則 5 強制 contract DTO error code 必須來自 `*FailureCodes` 常數；架構測試可加驗 contract assembly 不出現裸字串 error code。
- Handler 需要在 BC 邊界明確轉換 contract validation failure。
  - Mitigation: 此轉換是 BC 邊界責任的一部分（與 ADR-004 §2 「邊界 logger 補診斷 context」對齊），範例放在 `.claude/references/dotnet/exceptions.rule.md`。

---

## Alternatives Considered

### Alternative A: Repository 回傳 `Result<T, Failure>`

Rejected.

這會把 infrastructure exception 與 business failure 混在同一層處理，也會迫使每個 Repository 定義 technical failure code。對目前專案而言過度設計。

### Alternative B: 跨 BC contract 一律回傳 `Result<T, Failure>`

Rejected.

這會讓 `SharedKernel/Contracts` 的 contract 綁定 application error handling pattern，降低 BC contract 的獨立性。

### Alternative C: 保留 optional Repository wrapping pattern

Rejected.

目前 codebase 沒有使用此模式。保留 optional pattern 會讓後續實作與 code review 產生歧義。

---

## Implementation Rules

1. Repository methods return raw type only.
2. Repository does not catch `DbException` to create `Failure`.
3. BC internal Application Service / Handler returns `Result<T, Failure>`.
4. `SharedKernel/Contracts` cross-BC interfaces may return contract DTO.
5. Contract DTO error codes must come from `*FailureCodes` constants.
6. Consuming Handler converts contract DTO error code to `Failure`.
7. Endpoint / Controller maps `Failure.Code` to HTTP status code.
8. `UnhandledExceptionMiddleware` handles unexpected exceptions at HTTP boundary.
9. 任何提案修改 1–8，必須先開新 ADR。
