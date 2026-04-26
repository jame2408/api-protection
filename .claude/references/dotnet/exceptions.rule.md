# .NET 錯誤處理規則

本專案的錯誤處理模式與最佳實踐。

---

## A. Result Pattern（專案主要模式）

> ⚠️ **重要**：本專案 Service 層 **必須** 使用 `Result<T, Failure>` 回傳，**不可** 拋出例外處理商業邏輯錯誤。

### 基本用法

```csharp
// ✅ Service 方法必須回傳 Result
public async Task<Result<OrderResponse, Failure>> GetOrderAsync(int orderId, CancellationToken cancel)
{
    var order = await _repository.GetByIdAsync(orderId, cancel);
    if (order is null)
    {
        return FailureProvider.CreateFailure(GetOrderFailureCodes.OrderNotFound);
    }
    
    return new OrderResponse { Id = order.Id, Total = order.Total };
}

// ❌ 禁止在 Service 層拋出例外處理商業邏輯
public async Task<OrderResponse> GetOrderAsync(int orderId)
{
    var order = await _repository.GetByIdAsync(orderId);
    if (order is null)
    {
        throw new OrderNotFoundException(orderId); // ❌ 禁止
    }
    return new OrderResponse { Id = order.Id };
}
```

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
var result = await _service.GetOrderAsync(orderId, cancel);

if (result.IsFailure)
{
    return this.Failure(result.Error);
}

var order = result.Value;
// 繼續處理...

// ✅ 鏈式檢查
var userResult = await _userService.GetUserAsync(userId, cancel);
if (userResult.IsFailure)
{
    return userResult.Error; // 直接回傳 Failure
}

var orderResult = await _orderService.CreateOrderAsync(userResult.Value, cancel);
if (orderResult.IsFailure)
{
    return orderResult.Error;
}
```

### Controller 回傳

```csharp
// ✅ Controller 使用 SystemController.Failure() 方法
[HttpGet("{id}")]
public async Task<IActionResult> GetOrder(int id, CancellationToken cancel)
{
    var result = await _orderService.GetOrderAsync(id, cancel);
    
    if (result.IsFailure)
    {
        return this.Failure(result.Error);
    }
    
    return Ok(result.Value);
}

// ✅ 直接建立 Failure（權限檢查等）
if (!User.Identity?.IsAuthenticated ?? true)
{
    return this.Failure(FailureProvider.CreateFailure(GetOrderFailureCodes.Unauthorized));
}
```

---

## B. 例外處理（僅限基礎設施層）

> 只有 Repository 層或呼叫外部 API 時才使用 try-catch，且必須轉換為 Result。

### Repository 層

```csharp
// ✅ Repository 捕捉 DB 例外並轉換為 Failure
public async Task<Result<Order, Failure>> GetByIdAsync(int id, CancellationToken cancel)
{
    try
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancel);
        var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == id, cancel);
        
        if (order is null)
        {
            return FailureProvider.CreateFailure(OrderRepositoryFailureCodes.NotFound);
        }
        
        return order;
    }
    catch (DbException ex)
    {
        _logger.LogError(ex, "資料庫查詢失敗: OrderId={OrderId}", id);
        return FailureProvider.CreateFailure(OrderRepositoryFailureCodes.DatabaseError);
    }
}
```

### 外部 API 呼叫

```csharp
// ✅ ApiClient 捕捉 HTTP 例外
public async Task<Result<ExternalResponse, Failure>> CallExternalApiAsync(CancellationToken cancel)
{
    try
    {
        var response = await _httpClient.GetAsync("/api/data", cancel);
        response.EnsureSuccessStatusCode();
        
        var data = await response.Content.ReadFromJsonAsync<ExternalResponse>(cancel);
        return data!;
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "外部 API 呼叫失敗");
        return FailureProvider.CreateFailure(ExternalApiFailureCodes.ExternalServiceError);
    }
}
```

> 例外的 `ex.Message` 不會進入 `Failure`（Failure 只有 Code）。診斷資訊由 boundary
> logger 透過 `_logger.LogError(ex, ...)` 處理，Failure 僅向上傳遞穩定的 code。

---

## C. 全域例外處理

本專案使用 `UnhandledExceptionMiddleware` 處理未捕捉的例外：

```csharp
// 自動將未處理例外轉換為 Failure 回應
```

---

## D. Code Review Detection Patterns

| Issue | Pattern to Detect | Severity |
|-------|-------------------|----------|
| **Service 拋例外** | Service 層使用 `throw` 處理商業邏輯 | 🔴 Critical |
| **new Failure()** | 直接建立 `new Failure()` 而非使用 FailureProvider | 🔴 Critical |
| **裸字串 Failure code** | `CreateFailure("FOO")` 而非引用 `*FailureCodes.Foo` | 🟡 Warning |
| **未檢查 Result** | 直接使用 `.Value` 未檢查 `.IsFailure` | 🔴 Critical |
| **Empty Catch** | `catch { }` 或 `catch (Exception) { }` 不處理 | 🔴 Critical |
| **throw ex** | `throw ex;` 而非 `throw;`（遺失堆疊追蹤） | 🟡 Bug |
| **Controller 拋例外** | Controller 使用 `throw` 而非 `this.Failure()` | 🟡 Warning |

---

## E. Failure Code 常數位置

| 範圍 | 位置 | 範例 |
|------|------|------|
| Slice/BC 內部 | BC slice 資料夾內 | `KeyLifecycle/CreateApiKey/CreateApiKeyFailureCodes.cs` |
| 跨 BC contract | contract 同一資料夾 | `SharedKernel/Contracts/ConsumerValidationFailureCodes.cs` |
| Common（多 BC 共用） | 不預先建立 | 真正出現第二個使用者時再抽 |

> 沒有 `ErrorCode` enum — 本專案 `Failure.Code` 是 `string`。
> Code 字面值（例如 `"TENANT_NOT_FOUND"`）為 HTTP 回應的穩定 contract，常數命名（PascalCase）只是 IDE 端的取用便利。
