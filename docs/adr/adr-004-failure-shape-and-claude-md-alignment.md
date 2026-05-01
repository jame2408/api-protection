# Failure 形狀單欄位定案 與 CLAUDE.md 對齊

> 本 ADR 終結 `CLAUDE.md` 與 `record Failure(string Code)` 之間的長期矛盾，並把「診斷 context 由邊界 logger 處理」明文化。

---

## Status

Accepted (2026-05-01)

Superseded the contradictory wording at `CLAUDE.md:93`. CLAUDE.md edited in the same commit; `FailureProvider.CreateFailure` guard added in the same commit.

---

## Context

`CLAUDE.md:93` 寫：

> NEVER inject `ILogger` into Service or Domain layers. Embed diagnostic context (entity IDs, input values) into the `Failure` message or metadata so boundary loggers (Middleware, Pipeline Behavior) can produce meaningful logs without service-layer coupling.

但 `backend/src/SharedKernel/Domain/Failure.cs` 是：

```csharp
public record Failure(string Code);
```

`Failure` 只有單欄位 `Code`，**沒有 message、沒有 metadata**。`FailureProvider.CreateFailure(string code)` 也只接受 code。`exceptions.rule.md` §A、ADR-003 與所有 `*FailureCodes` 常數設計，都是以「Failure 只有 Code」為前提。

矛盾的後果：

- AI agent 讀 `CLAUDE.md` 後，可能嘗試把「entity ID」或「input value」塞進 Failure，會發現 API 對不上而困惑。
- Code review 也會出現「這個 Failure 為什麼沒帶 metadata？」的無效質疑。
- ADR-003 §B「Repository 不包 Failure」的論述是建立在「Failure 不該攜帶診斷 context」之上，但 `CLAUDE.md` 卻反過來要求把診斷 context 塞進去。

需要決定：(1) 改 `CLAUDE.md` 對齊 `Failure` 設計，或 (2) 改 `Failure` 設計對齊 `CLAUDE.md`。

---

## Decision

### 1. 保留 `record Failure(string Code)` 為單欄位設計

`Failure` 維持 `record Failure(string Code)`，不擴充 message / metadata 欄位。

### 2. 診斷 context 在邊界 logger 補上，不放進 Failure

- Endpoint / Middleware / Pipeline Behavior / Background Service 是允許注入 `ILogger<T>` 的邊界。
- 這些邊界拿到 `Failure.Code` 之後，從 `HttpContext`、Command 物件、Query 物件取得 entity ID、input value、tenant ID 等診斷 context，自行 structured log。
- Service / Domain / Handler 只向上傳 `Failure.Code`，不負責 logging。

### 3. 修正 `CLAUDE.md:93`

新文字：

> CRITICAL: Service layer uses `Result<T, Failure>` — NEVER `throw` for business logic.
> NEVER use `new Failure()` — all failures created via `FailureProvider.CreateFailure()`.
> NEVER access `.Value` without checking `.IsFailure` first.
> NEVER use empty catch blocks; NEVER use `throw ex;` (use `throw;`).
> NEVER inject `ILogger` into Service, Domain, or Handler layers. Diagnostic context (entity IDs, input values, tenant scope) is captured at the boundary — Endpoint, Middleware, Pipeline Behavior, or Background Service — by reading `HttpContext`, the inbound Command, or the Query object. `Failure.Code` is the only thing Service / Handler propagates upward; it is a stable string contract, never a free-form message.

### 4. Architecture test 鎖死 `Failure` shape（示意）

當 `Architecture.Tests` 從空殼拉起後（todo.md #20），加一條結構鎖。NetArchTest fluent API 並沒有 `NotHavePropertyOtherThan(...)` 這個原語，因此實作時請選下列其中一種：

(a) 直接寫 xUnit reflection 測試（最小可行）：

```csharp
[Fact]
public void Failure_HasOnlyCodeProperty()
{
    var props = typeof(Failure).GetProperties();
    props.Should().ContainSingle()
         .Which.Name.Should().Be(nameof(Failure.Code));
}
```

(b) 自製 `ICustomRule` 包裝給 NetArchTest 使用，再以 `Types.InAssembly(...).That().HaveName("Failure").Should().MeetCustomRule(new SingleCodePropertyRule())` 套用。

任一寫法都會在有人加欄位（`Message` / `Metadata` / `Detail` / `Context`）時讓測試 red。**上面的範例是說明意圖，不是可貼上即用的最終實作。**

---

## Rationale

### 為什麼選擇單欄位

- **Failure.Code 是穩定的 wire-format / log-format contract**。RFC 9457 ProblemDetails 的 `errorCode` 欄位、SIEM 警報的 grouping key、metric 的 dimension，全部都吃這條字串。
- **加欄位破壞穩定性**。若 Failure 有 `Message` 或 `Metadata`，這些值的格式就會變成隱性 contract，將來修文字會破 SIEM 規則。
- **診斷 context 在邊界產生才正確**。Service 層不該知道 HTTP request、tenant claim、trace ID — 那是邊界資訊。讓 boundary logger 補才是責任分離。
- **與 ADR-003 一致**。ADR-003 第 4 條「HTTP boundary 負責 response mapping」與第 8 條 `UnhandledExceptionMiddleware` 都已經承擔了「邊界做事」的職責。

### 為什麼不擴充 Failure

替代方案 A 是把 `Failure` 改成 `record Failure(string Code, string? Message, IReadOnlyDictionary<string, object>? Metadata)`。問題：

- 大量現有 `*FailureCodes.X` 引用會需要決定 message / metadata 的「正確值」，但實際上沒有合理值（business code 拿不到 entity ID）。
- 會誘惑工程師把 raw exception message 塞進 `Message`，反而把 raw infrastructure detail 帶到回應，違反 ADR-003 §B 的 unhandled exception 不外洩設計。
- ADR-003、`exceptions.rule.md` §A / §B / §D、所有 `*FailureCodes` 常數設計都建立在單欄位前提上。動 `Failure` 就要連動四份文件 + 一份 detection table。成本高、收益低。

---

## Consequences

### Positive

- `CLAUDE.md` 與 production code 一致，AI agent 不再被矛盾敘述誤導。
- Architecture test 鎖死 shape，未來 PR 加欄位會自動 fail。
- `*FailureCodes` 常數設計與 ADR-003 的責任分工保持簡潔。
- 邊界 logger 的責任更清楚 — 它有 HTTP context、它應該 log。

### Negative / Trade-offs

- Service / Handler 想做「攜帶診斷 context 用於 debug」會更麻煩 — 必須去邊界做。
  - Mitigation: `IPipelineBehavior` 模式（請見 MediatR / 自製版）可以在 Handler 邊界 wrap 一層自動 log，不需要每個 endpoint 重複寫。
- 現有 `CLAUDE.md` 讀者（包括人類維護者）需要重新理解「為什麼診斷 context 不在 Failure 裡」。
  - Mitigation: 本 ADR 即為正式文件，rule.md 也已對齊。

---

## Alternatives Considered

### Alternative A: 擴充 `Failure` 為 `record Failure(string Code, string? Message, IReadOnlyDictionary<string, object>? Metadata)`

Rejected. 上面 Rationale 已詳細說明：誘導工程師把 infrastructure raw message 塞進 message 欄位、破壞 wire-format contract 穩定性、所有現有 `*FailureCodes` 引用都要重新決定 metadata 值，連動成本高。

### Alternative B: 加 `Failure.WithContext(string key, object value)` builder pattern

Rejected. 雖然不破壞 record shape，但本質上仍把診斷責任拉回 Service 層，違反「邊界 logger 補上」的責任分離。也讓 Failure code 變得「有時帶 context 有時沒帶」，wire format / log format 更不穩定。

### Alternative C: 引入 `Result<T, Failure>` 之外的 `Result<T, FailureDetails>` 型別，BC 內部用 detail 版本

Rejected. 兩種 Result 類型會讓 BC 內外的轉換變成雜訊，cross-BC contract 也會反覆被質疑該用哪個版本。ADR-003 已採用「跨 BC contract 用 contract DTO（如 `ConsumerValidationResult`）」的解法，再開第二條 Result 軌會讓這層更亂。

---

## Implementation Rules

1. `Failure` 永遠是 `record Failure(string Code)` — 單一字串欄位。
2. `FailureProvider.CreateFailure(string code)` 是唯一允許的 Failure 建立方式，並必須以 `ArgumentException.ThrowIfNullOrWhiteSpace(code)` 拒絕 null / whitespace。Failure code 是穩定 contract，呼叫端不應有合法理由傳入空白值。
3. Service / Domain / Handler 不注入 `ILogger<T>`。
4. 診斷 context 由邊界 logger（Endpoint / Middleware / Pipeline Behavior / Background Service / Hosted Service）從 `HttpContext`、Command、Query 物件補上。
5. `CLAUDE.md` 寫法須對齊 §3 的決策。
6. `Architecture.Tests` 加一條結構鎖死測試（reflection 或 NetArchTest custom rule，見 §4）禁止 `Failure` 增加欄位。
7. 任何提案修改 1–6，必須先開新 ADR。
