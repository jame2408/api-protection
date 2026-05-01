# Primary Constructor 為預設「依賴注入」方式，field-based injection 為例外

> 本 ADR 固化「Handler / Service / Repository / Middleware 透過 Primary Constructor 接收依賴；Endpoint 走 Minimal API delegate parameter injection」這個既成事實，並把它與「class 內部 field 的命名規則」、「test fixture 的 mock field」明確切開：本 ADR 只規範 **production code 的依賴注入寫法**，不規範「class 是否能擁有 field」、不規範 test fixture、也不修改 `naming.guide.md` 的 `_camelCase` 規則。

---

## Status

Accepted (2026-05-01)

§6 同步項目（testing.guide.md §C/§E caveat、di.rule.md / naming.guide.md settings snapshot 註解）已在同 commit 落地。

---

## Context

### 現況

本專案 production code 中：

- **Handler / Service / Repository / Middleware**：以 Primary Constructor 接收依賴。`ApiKeyRepository`、`AccessPolicyRepository`、`ScopeRegistryService`、`ConsumerValidatorService`、`CreateApiKeyHandler`、`UnhandledExceptionMiddleware` 全部如此，**零 field-based DI**。
- **Endpoint**：Minimal API `static class` + `Map(IEndpointRouteBuilder app)` + route handler delegate parameter injection。例如 `CreateApiKeyEndpoint` 的 `app.MapPost(..., async (string tenantId, Request request, ICreateApiKeyHandler handler, CancellationToken cancel) => …)`。Endpoint **不使用 constructor**（static class 沒有 instance）、**不使用 field-based DI**，依賴在 delegate 參數列。

這個風格在多次 hardening commit（`c31d725` / `acfdb95` / `3b0b10d`）逐步落地，但選擇本身從未成文。`.claude/references/dotnet/*.rule.md` / `*.guide.md` 過去多份範例曾使用 `_orderRepository.X` / `_userService.Y` / `_httpClient` 形式 —— 這些**都是 production-style field-based DI 範例**。Hardening commits 已將 `exceptions.rule.md` / `async.rule.md` / `security.rule.md` / `linq.rule.md` / `ef-core.rule.md` 範例改寫為 Primary Constructor 形式。`testing.guide.md` 仍保有 `_orderRepository` / `_service` 等用法，但這些是 **test fixture private mock field** 風格（見 §C 與 §E），與 production field-based DI 是不同議題（見 §3.2）。本 ADR 不擴張到 test fixture。

### 三個容易被混淆的概念

本 ADR 的觸發點是過去的 reference docs drift；起草初稿時曾把規則寫成「private field 為例外」，這是不準確的擴張。釐清如下：

| 概念 | 是什麼 | 命名規則來源 | 本 ADR 是否規範 |
|---|---|---|---|
| **Production field-based DI** | Production class 用 `private readonly IFoo _foo;` 接收建構式參數，再 `_foo = foo;` | `naming.guide.md` `_camelCase` | ✅ 規範：避免，改用 Primary Constructor |
| **Production class 內部狀態 field** | `private readonly Channel<T> _queue;` / `private readonly SemaphoreSlim _gate;` / `private long _lastTickAt;` 等與 DI 無關的狀態欄位 | `naming.guide.md` `_camelCase` | ❌ 不規範。本 ADR 不限制 production class 是否能持有 field、不要求例外註解、不限縮命名規則 |
| **Test fixture mock field** | xUnit / NSubstitute 測試類別的 `private readonly IFoo _foo = Substitute.For<IFoo>();` | `naming.guide.md` `_camelCase` | ❌ 不規範。Test fixture 是測試框架慣例，本 ADR 不擴張到 `backend/tests/` |

`naming.guide.md` 的 `_camelCase` 規則是「**當使用 field 時**該如何命名」，本 ADR 不修改、不旁註、不附加條件。

### 不固化會發生什麼

未來新 BC / 新 hardening pass / 新 reference docs 若再出現 production-style `_repository.X` 形式，就會：

- AI agent 啟動時讀範例，傾向學該形式寫程式。
- 與既有 production code 風格不一致，code review 容易來回。
- 重複前次 hardening pass 的「掃 production `_xxx`」工時。

---

## Decision

### 1. Production class 預設依賴注入方式為 Primary Constructor

所有 `Handler` / `Service` / `Repository` / `Middleware` 透過 Primary Constructor 接收依賴：

```csharp
public class CreateApiKeyHandler(
    IConsumerValidator consumerValidator,
    IApiKeyRepository keyRepository,
    IScopeRegistry scopeRegistry,
    IAccessPolicyService accessPolicyService) : ICreateApiKeyHandler
{
    public async Task<Result<CreateApiKeyResponse, Failure>> HandleAsync(
        CreateApiKeyCommand command, CancellationToken cancel = default)
    {
        var validation = await consumerValidator.ValidateAsync(...);
        // ...
    }
}
```

method body 直接以參數名（`consumerValidator`、`keyRepository`…）使用依賴。

### 2. Endpoint 走 Minimal API delegate parameter injection

Endpoint 不在 §1 範圍。本專案 Endpoint 寫法：

```csharp
public static class CreateApiKeyEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys",
            async (
                string tenantId,
                string consumerId,
                Request request,
                ICreateApiKeyHandler handler,           // ← DI 在 delegate 參數列
                CancellationToken cancel) =>
            {
                var result = await handler.HandleAsync(...);
                // ...
            });
    }
}
```

Endpoint **不使用** instance class、**不使用** constructor、**不使用** field-based DI。Endpoint 依賴注入由 ASP.NET Core route handler delegate 參數解析完成。任何把 Endpoint 改寫為 instance class + Primary Constructor 的提案都不在本 ADR 鼓勵範圍。

### 3. Field-based DI 是例外，需要正當理由

下列場景因 Primary Constructor 表達不便、需要以 field 持有「來自 DI 的依賴或其衍生物」，視為例外，必須在程式碼註解寫明理由：

- **Lazy / 延後實例化**：`private readonly Lazy<HttpClient> _client = new(() => factory.CreateClient("x"));` 必須在建構期之後才能初始化。
- **Settings snapshot**：從 `IOptions<T>` 或 `EnvironmentVariable` 取出 immutable copy 存為 field，避免每次讀取重新解析。
- **Disposable 訂閱 / handle**：例如 `IDisposable` event subscription、`CancellationTokenSource`，必須以 field 持有以便 `Dispose` 時釋放。

例外情形仍依 `naming.guide.md` 的 `_camelCase` 慣例命名 —— 本 ADR 不修改該規則。

### 3.1. Production class 內部狀態 field 不在本 ADR 範圍

只要不是「接收 DI」，production class 內部欄位（mutable cursor、queue、gate、counter、cache、handle、lock 物件、`Result<T>` 的 `_value` / `_error`、`AggregateRoot<TId>` 的 `_domainEvents` 等）一律合法、不需例外註解、命名照 `naming.guide.md` `_camelCase`。本 ADR 對此**沒有任何限制**。

例：

```csharp
public class TimedRotationWorker(IRotationScheduler scheduler) : BackgroundService
{
    private long _lastTickAt;                      // 內部狀態，本 ADR 不規範
    private readonly SemaphoreSlim _gate = new(1); // 內部狀態，本 ADR 不規範

    protected override async Task ExecuteAsync(CancellationToken cancel)
    {
        // 同時使用：DI 參數 scheduler（Primary Ctor）+ 內部狀態 _lastTickAt / _gate
    }
}
```

### 3.2. Test fixture mock field 不在本 ADR 範圍

`backend/tests/` 下的測試類別使用 xUnit / NSubstitute fixture pattern：

```csharp
public class CreateApiKeyHandlerTests
{
    private readonly IConsumerValidator _consumerValidator = Substitute.For<IConsumerValidator>();
    private readonly IApiKeyRepository _keyRepository = Substitute.For<IApiKeyRepository>();
    private readonly CreateApiKeyHandler _handler;

    public CreateApiKeyHandlerTests()
    {
        _handler = new CreateApiKeyHandler(_consumerValidator, _keyRepository, ...);
    }
}
```

這些 `_consumerValidator` / `_keyRepository` 是**測試框架的 fixture private state**，不是 production field-based DI；它們是 NSubstitute mock 物件，作用域僅限該測試類別實例。本 ADR 不要求 test fixture 改寫成 Primary Constructor 形式（xUnit `[Fact]` 方法不能接收建構式注入的 mock）。

`testing.guide.md` 的 `_orderRepository.X` / `_service.X` 範例屬此類，**不違反本 ADR**。但為避免 AI agent 誤把 fixture 風格學去寫 production code，`testing.guide.md` §C 應加一段 caveat（見 §4）。

### 4. Reference docs 範例風格

**Production-targeted reference docs**（`exceptions.rule.md` / `async.rule.md` / `security.rule.md` / `linq.rule.md` / `ef-core.rule.md` / `di.rule.md` / `naming.guide.md`）：範例 body 使用 `repository.X` / `service.X` 形式（Primary Constructor 參數名直呼）。**保留 `_xxx` 的範例僅限**：示範 §3 例外場景（Lazy / Settings snapshot / Disposable handle）、示範「class 內部狀態 field 命名」（§3.1）、或明確標示為 ❌ / anti-pattern 的反例（例如 `di.rule.md` 的 Captive Dependency `_scopedService` field —— 反例本身就是要展示錯誤寫法，不是推薦 production pattern）。Settings snapshot 範例**必須**在 field 上方加註解，示範規則要求的「例外註解」（見 §6 同步項目）。

**Test-targeted reference docs**（`testing.guide.md`）：保留現有 fixture / mock 風格範例（`_orderRepository` / `_service`），但須在 §C / §E 開頭加一段 caveat：

> 以下範例中的 `_orderRepository` / `_service` 是 xUnit / NSubstitute test fixture 的 private mock field（見 §D Test Class 結構），不是 production field-based DI。Production code 請見 `di.rule.md` 與 `exceptions.rule.md` 的 Primary Constructor 範例。

`naming.guide.md` 既有寫法（含 `_camelCase` 那條）**維持不動** —— 命名規則與本 ADR 的 DI 寫法決策正交。

### 5. 本規則不做機械化驗證

不加 NetArchTest、不加 Roslyn analyzer，原因：

- 唯一可機械化偵測的訊號是「class 同時擁有顯式建構式且該建構式僅做 `_x = x;` 賦值」這個 pattern，雖可寫但成本超過收益。
- 對「`private readonly Foo _foo;`」這個 regex 做掃描會誤傷合法的 §3.1 內部狀態 field、`Result<T>._value` / `AggregateRoot._domainEvents` 等 —— 起草初稿曾打算這樣做，現撤回。
- 本規則靠 (a) AI agent 啟動載入規範、(b) Reference docs 範例風格一致、(c) Code review 一條 checklist：「production class 接收依賴是否使用 Primary Constructor？Endpoint 是否走 Minimal API delegate parameter？」即可推動。

未來若仍頻繁 drift 再考慮 IDE0290 強化。

### 6. 本 ADR 接受時的同步項目

接受本 ADR 時，下列 reference docs 必須在同 commit 同步：

1. `testing.guide.md` §C 與 §E 開頭加 caveat 段落（見 §4），明示 `_orderRepository` / `_service` 是 test fixture mock field，不是 production pattern。
2. `di.rule.md` §B `CacheService` settings snapshot 範例的 `_connectionString` 上方加註解：`// ADR-005 §3 settings snapshot exception: cache the env var value once at construction.`
3. `naming.guide.md` §F 的 `CacheService` settings snapshot 範例同步 #2 註解。

`naming.guide.md` 既有命名規則表本身**不修改**（包括 `_camelCase` 那條）。

---

## Rationale

### 為何只規範 production DI 寫法、不規範「class 是否能用 field」、不規範 test fixture

- **Field 是物件持有非建構期狀態的正常手段**，禁止它會讓 lazy 容器、Background Service cursor、queue、gate、`Result<T>` internal state、`AggregateRoot._domainEvents` 等合法場景被逼出彆扭寫法。
- **Test fixture 的 `_mock` field 是 xUnit / NSubstitute 慣例**，xUnit 每個 `[Fact]` 都會 new 一份 test class instance，fixture field 是該 instance 的私有狀態，作用域與生命週期都與 production class 不同；強加 production 規則會破壞 test 風格、也無法用 Primary Constructor 替代（因為 mock 本身就是要在 fixture 中 `Substitute.For<T>()` 建立）。
- 本 ADR 的目標是固化「production class 接收依賴的方式」這個過去 drift 過的決策，不擴張到 class 形狀、也不擴張到測試。

### 為何把 Endpoint 從 Primary Constructor 切開

Endpoint 是 Minimal API `static class`，static class 沒有 instance、沒有 constructor、也不能有 instance field。把 Endpoint 列入 §1 會誘導 AI agent 把 Endpoint 改寫為 instance class，破壞既有 Minimal API slice 模式。Endpoint 的 DI 路徑是 ASP.NET Core route handler delegate 的參數解析，與 Primary Constructor 是兩種機制。

### 為何不修改 `naming.guide.md`

`_camelCase` 是 Microsoft 標準命名慣例，命名規則的觸發條件是「使用 field 時」，與「是否使用 field 接收依賴」是不同層次的問題。在 naming 文件旁註「僅例外使用」會讓命名規則被 DI 決策污染，未來若移除本 ADR、命名規則也會被連帶搞亂。

### 為何不機械化

機械化偵測的真實成本是誤報。對「class 內部狀態 field」誤報會逼工程師為合法寫法補例外註解，noise 比實際抓到的 field-based DI 還高。先靠範例 + review 推動，留待真有再次 drift 時再強化工具。

---

## Consequences

### Positive

- 範例與 production 一致，AI agent 不再學到 production-style field-based DI。
- Endpoint 的 Minimal API 模式被明文保護，不會被誤改為 instance class。
- 「依賴注入寫法」「field 命名規則」「test fixture」三者明確切分，不互相污染。
- 減少未來 hardening pass 的「掃 production `_xxx`」工時。

### Negative / Trade-offs

- 微幅違反 Microsoft 通用 .NET 範例風格（部分 Microsoft Learn / 舊 sample 仍用 field-based DI）。
  - Mitigation: 本 ADR 即為解釋來源；reference docs 範例已大致切換，§6 列出剩餘同步項目。
- 沒有 Roslyn / NetArchTest 自動偵測 — 規則靠 code review 與範例一致。
  - Mitigation: 若再次 drift，再加 IDE0290 analyzer。

---

## Alternatives Considered

### Alternative A: 把規則寫成「禁止 private field」或「private field 為例外」

Rejected. 這是 ADR 起草初稿的寫法，會把規則範圍誤從「DI 寫法」擴張到「class 內部狀態」。lazy 容器、queue、gate、cursor、`Result<T>._value`、`AggregateRoot._domainEvents` 等合法場景會被連帶汙名化。

### Alternative B: 在 `naming.guide.md` 旁註「`_camelCase` 僅在例外使用」

Rejected. naming 規則的觸發條件是「當使用 field 時」，不該被 DI 決策連帶限縮。命名與 DI 是兩個正交的問題。

### Alternative C: 全面禁止 field-based DI 並加 IDE0290 強制

Rejected for now. 工具成本不高，但 §3 的合法例外也會被 analyzer 標紅，需要逐一 `#pragma` 豁免，noise 高於收益。先靠範例 + review，未來真有再次 drift 才升級。

### Alternative D: 撤回本 ADR，僅在 `naming.guide.md` 加一行「依賴用 Primary Constructor」

Rejected. 撤回後決策論述只剩一行 reference 文字，未來新人 / AI agent 看到 .NET 9 之前的舊 sample 仍會回退 field-based DI，缺乏可引用的 ADR 反駁。

### Alternative E: 把 Endpoint 也改為 instance class + Primary Constructor

Rejected. Minimal API 的 DI 由 ASP.NET Core route handler 參數解析完成，instance class 是不必要的中介層，也與既有 Minimal API slice 結構不一致。

### Alternative F: ADR 涵蓋 test fixture，禁止 `_mock` field

Rejected. xUnit `[Fact]` 不能接收建構式注入，test fixture 的 `_mock` field 是框架慣例的必要寫法。強行統一會讓測試難寫且不可讀。

---

## Implementation Rules

1. Handler / Service / Repository / Middleware 接收依賴一律用 Primary Constructor。
2. Endpoint 走 Minimal API `static class` + `Map(IEndpointRouteBuilder app)` + route handler delegate parameter injection；不使用 constructor、不使用 field-based DI。
3. Field-based DI 是 production 例外，僅限：Lazy / 延後實例化、Settings snapshot、Disposable handle / subscription。例外必須在程式碼註解寫明理由。
4. Production class 內部狀態 field（與 DI 無關的 cursor / queue / gate / counter / cache / handle / lock / aggregate domain events / Result internal state）**不在本 ADR 範圍**，命名照 `naming.guide.md` `_camelCase`，不需例外註解。
5. **Test fixture mock field**（`backend/tests/` 下 xUnit / NSubstitute private fields）**不在本 ADR 範圍**。
6. `.claude/references/dotnet/*.rule.md` / `*.guide.md` 的 production-targeted 範例 body 一律使用 Primary Constructor 參數名直呼形式；保留 `_xxx` 的範例僅限：§3 例外、§3.1 內部狀態、或明確標示為 ❌ / anti-pattern 的反例。Settings snapshot 範例必須附「例外原因」註解。
7. **不修改** `naming.guide.md` 既有規則（含 `_camelCase` 那條）。本 ADR 與命名規則正交。
8. 不加 NetArchTest / Roslyn analyzer 強制。靠範例一致 + code review 推動。
9. 任何提案修改 1–8，必須先開新 ADR。
