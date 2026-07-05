# .NET 錯誤處理規則

本專案的錯誤處理模式與最佳實踐。

---

## A. Result Pattern（專案主要模式）

> ⚠️ **重要**：本專案 BC 內部 Application Service / Handler **必須** 使用 `Result<T, Failure>` 回傳，**不可** 拋出例外處理商業邏輯錯誤。

### 基本用法

```csharp
// ✅ Handler / Service 方法必須回傳 Result（Primary Constructor 注入，body 內無 _ 前綴）
public class GetOrderHandler(IOrderRepository repository) : IGetOrderHandler
{
    public async Task<Result<OrderResponse, Failure>> HandleAsync(
        int orderId, CancellationToken cancel = default)
    {
        var order = await repository.GetByIdAsync(orderId, cancel);
        if (order is null)
        {
            return FailureProvider.CreateFailure(GetOrderFailureCodes.OrderNotFound);
        }

        return new OrderResponse { Id = order.Id, Total = order.Total };
    }
}

// ❌ 禁止在 Service 層拋出例外處理商業邏輯
public class BadGetOrderHandler(IOrderRepository repository) : IGetOrderHandler
{
    public async Task<OrderResponse> HandleAsync(int orderId)
    {
        var order = await repository.GetByIdAsync(orderId);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId); // ❌ 禁止
        }

        return new OrderResponse { Id = order.Id };
    }
}
```

### 跨 BC Contract 例外

位於 `SharedKernel/Contracts/` 的跨 BC 介面可以回傳 contract-specific DTO，而非 `Result<T, Failure>`。

```csharp
// ✅ 跨 BC contract 使用專用 DTO，避免讓 contract 綁定 consuming BC 的 Result shape
public interface IConsumerValidator
{
    Task<ConsumerValidationResult> ValidateAsync(
        string tenantId,
        string consumerId,
        CancellationToken cancel = default);
}

// ✅ Consuming Handler 在 BC 邊界轉成 Failure
var validation = await consumerValidator.ValidateAsync(command.TenantId, command.ConsumerId, cancel);
if (!validation.IsValid)
{
    return FailureProvider.CreateFailure(validation.ErrorCode!);
}
```

> 規則：
> - 例外範圍只限 `SharedKernel/Contracts/` 的跨 BC contract。
> - Contract DTO 必須使用穩定的 error code 字串；code 來源必須是 `*FailureCodes` 常數。
> - Consuming Handler 必須在 BC 邊界將 error code 轉為 `Failure`。
> - BC 內部 Application Service / Handler 仍必須回傳 `Result<T, Failure>`。

### Failure 建立規則

> 本專案 `Failure` 形狀為 `record Failure(string Code)` — 單一字串 Code 欄位，不含 message / metadata。
> Failure code 必須以 **per-BC 常數類別** 集中宣告，避免裸字串散落。

```csharp
// ✅ 使用 FailureProvider 建立 Failure，並引用 per-BC 常數
return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.KeyLimitExceeded);
return FailureProvider.CreateFailure(ConsumerValidationFailureCodes.TenantNotFound);

// ❌ 禁止直接 new Failure
return new Failure("KEY_LIMIT_EXCEEDED"); // ❌ 禁止

// ❌ 禁止裸字串（typo 風險、無 IDE 跳轉、無集中索引）
return FailureProvider.CreateFailure("KEY_LIMIT_EXCEEDED"); // ❌ 應改用常數

// ❌ 禁止假設不存在的多載（CreateFailure 只接受 string code）
return FailureProvider.CreateFailure(ErrorCode.NotFound, "找不到資源"); // ❌ 不存在的 enum / 多載
```

### Failure Code 常數宣告

```csharp
// BC-internal code → 放 BC slice 內
namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public static class CreateApiKeyFailureCodes
{
    public const string KeyLimitExceeded = "KEY_LIMIT_EXCEEDED";
    public const string KeyNameDuplicate = "KEY_NAME_DUPLICATE";
    public const string ScopeNotFound = "SCOPE_NOT_FOUND";
    public const string ValidationErrorPrefix = "VALIDATION_ERROR";
}

// Cross-BC contract code → 放 contract 附近（SharedKernel/Contracts）
namespace ApiKeyManagement.SharedKernel.Contracts;

public static class ConsumerValidationFailureCodes
{
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string TenantSuspended = "TENANT_SUSPENDED";
    public const string ConsumerNotFound = "CONSUMER_NOT_FOUND";
}
```

> 規則：
> - **不預先建立 Common code 常數類別** — 真正跨多 BC 共用時再抽。
> - 常數類別命名：`{BC 或 Slice}FailureCodes`，flat `public static class` + `public const string`。
> - Endpoint 分流時 `switch` arm 引用常數；前綴比對用 `StartsWith(CreateApiKeyFailureCodes.ValidationErrorPrefix)`。

### Result 檢查

```csharp
// ✅ 檢查 Result 並處理
var result = await handler.HandleAsync(command, cancel);

if (result.IsFailure)
{
    return result.Error;
}

var response = result.Value;
// 繼續處理...

// ✅ 鏈式檢查（同一 Handler 內呼叫多個 service / handler，service 透過 Primary Constructor 注入）
var userResult = await userService.GetUserAsync(userId, cancel);
if (userResult.IsFailure)
{
    return userResult.Error; // 直接回傳 Failure
}

var orderResult = await orderService.CreateOrderAsync(userResult.Value, cancel);
if (orderResult.IsFailure)
{
    return orderResult.Error;
}
```

### HTTP 邊界回傳

```csharp
// ✅ Minimal API endpoint 在 HTTP 邊界將 Failure.Code 對應為狀態碼
if (result.IsFailure)
{
    return result.Error.Code switch
    {
        ConsumerValidationFailureCodes.TenantNotFound   => Results.NotFound(new { error = result.Error.Code }),
        CreateApiKeyFailureCodes.KeyLimitExceeded       => Results.Conflict(new { error = result.Error.Code }),
        _ when result.Error.Code.StartsWith(CreateApiKeyFailureCodes.ValidationErrorPrefix) =>
            Results.BadRequest(new { error = result.Error.Code }),
        _ => Results.Problem(result.Error.Code)
    };
}
```

---

## B. 例外處理（Infrastructure / 外部邊界）

> Repository 不把 DB exception 包成 `Result<T, Failure>`。DB / infrastructure exception 屬於 unexpected failure，直接 bubble up，由 `UnhandledExceptionMiddleware` 統一記錄並轉成 5xx response。

### Repository 層

```csharp
// ✅ Repository 回傳 raw type，不 try-catch，不轉 Failure
public class ApiKeyRepository(AppDbContext db) : IApiKeyRepository
{
    public async Task SaveAsync(ApiKey apiKey, CancellationToken cancel = default)
    {
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancel);
    }

    public Task<int> CountActiveAsync(
        string consumerId,
        string environment,
        string tenantId,
        CancellationToken cancel = default)
        => db.ApiKeys.CountAsync(k =>
            k.ConsumerId == consumerId &&
            k.Environment == environment &&
            k.TenantId == tenantId &&
            k.Status == ApiKeyStatus.Active,
            cancel);
}
```

> 規則：
> - Repository 方法回傳 domain entity、primitive、collection、`Task` 等 raw type。
> - Repository 不回傳 `Result<T, Failure>`。
> - Repository 不 catch `DbException` 轉成 business `Failure`。
> - Handler 只處理可預期的 business guard / contract validation failure。

### 外部 API 呼叫

```csharp
// ✅ 外部 API boundary 可以把可預期的 remote failure 轉成穩定 Failure code
//    Infrastructure 邊界允許注入 ILogger（CLAUDE.md NEVER 規則只禁止 Service / Domain / Handler）
public class ExternalApiClient(
    HttpClient httpClient,
    ILogger<ExternalApiClient> logger) : IExternalApiClient
{
    public async Task<Result<ExternalResponse, Failure>> CallExternalApiAsync(
        CancellationToken cancel = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/api/data", cancel);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<ExternalResponse>(cancel);
            return data!;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "外部 API 呼叫失敗");
            return FailureProvider.CreateFailure(ExternalApiFailureCodes.ExternalServiceError);
        }
    }
}
```

> 例外的 `ex.Message` 不會進入 `Failure`（Failure 只有 Code）。診斷資訊由 boundary logger 透過 `logger.LogError(ex, ...)` 處理，Failure 僅向上傳遞穩定的 code。

---

## C. 全域例外處理

本專案使用 `UnhandledExceptionMiddleware` 處理未捕捉的例外：

```csharp
// ✅ 未處理例外只在 HTTP boundary 轉成 generic Failure response
app.UseMiddleware<UnhandledExceptionMiddleware>();
```

---

## D. Code Review Detection Patterns

| Issue | Pattern to Detect | Severity |
|-------|-------------------|----------|
| **Service 拋例外** | Service 層使用 `throw` 處理商業邏輯 | 🔴 Critical |
| **Service 未回 Result** | BC 內部 Application Service / Handler 未回傳 `Result<T, Failure>` | 🔴 Critical |
| **跨 BC Contract 誤用 Result** | `SharedKernel/Contracts` 介面強迫回傳 `Result<T, Failure>` | 🟡 Warning |
| **Repository 包裝 Result** | Repository 方法回傳 `Result<T, Failure>` 或 catch `DbException` 轉 business `Failure` | 🟡 Warning |
| **new Failure()** | 直接建立 `new Failure()` 而非使用 FailureProvider | 🔴 Critical |
| **裸字串 Failure code** | `CreateFailure("FOO")` 而非引用 `*FailureCodes.Foo` | 🟡 Warning |
| **未檢查 Result** | 直接使用 `.Value` 未檢查 `.IsFailure` | 🔴 Critical |
| **Empty Catch** | `catch { }` 或 `catch (Exception) { }` 不處理 | 🔴 Critical |
| **throw ex** | `throw ex;` 而非 `throw;`（遺失堆疊追蹤） | 🟡 Bug — 已機械化：CA2200（ADR-016），build 即攔，AI review 免查 |
| **HTTP 邊界拋例外** | Endpoint / Controller 使用 `throw` 處理商業邏輯 | 🟡 Warning |

> `Service 未回 Result` 不適用於 `SharedKernel/Contracts/` 的跨 BC contract DTO 例外。

---

## E. Failure Code 常數位置

| 範圍 | 位置 | 範例 |
|------|------|------|
| Slice/BC 內部 | BC slice 資料夾內 | `KeyLifecycle/CreateApiKey/CreateApiKeyFailureCodes.cs` |
| 跨 BC contract | contract 同一資料夾 | `SharedKernel/Contracts/ConsumerValidationFailureCodes.cs` |
| Common（多 BC 共用） | 不預先建立 | 真正出現第二個使用者時再抽 |

> 沒有 `ErrorCode` enum — 本專案 `Failure.Code` 是 `string`。
> Code 字面值（例如 `"TENANT_NOT_FOUND"`）為 HTTP 回應的穩定 contract，常數命名（PascalCase）只是 IDE 端的取用便利。
