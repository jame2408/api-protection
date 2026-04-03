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
        return FailureProvider.CreateFailure(ErrorCode.NotFound, "訂單不存在");
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

```csharp
// ✅ 使用 FailureProvider 建立 Failure（注入或靜態方法）
return FailureProvider.CreateFailure(ErrorCode.Unauthorized);
return FailureProvider.CreateFailure(ErrorCode.NotFound, "找不到資源");
return FailureProvider.CreateFailure(ErrorCode.ValidationError, "驗證失敗", new { Field = "email" });

// ❌ 禁止直接 new Failure
return new Failure(ErrorCode.NotFound, "找不到資源"); // ❌ 禁止
```

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
    return this.Failure(FailureProvider.CreateFailure(ErrorCode.Unauthorized));
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
            return FailureProvider.CreateFailure(ErrorCode.NotFound);
        }
        
        return order;
    }
    catch (DbException ex)
    {
        _logger.LogError(ex, "資料庫查詢失敗: OrderId={OrderId}", id);
        return FailureProvider.CreateFailure(ErrorCode.DatabaseError, ex.Message);
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
        return FailureProvider.CreateFailure(ErrorCode.ExternalServiceError, ex.Message);
    }
}
```

---

## C. 全域例外處理

本專案使用 `UnhandledExceptionMiddleware` 處理未捕捉的例外：

```csharp
// 已實作於 JobBank1111.Event.WebAPI.Middlewares.UnhandledExceptionMiddleware
// 自動將未處理例外轉換為 Failure 回應
```

---

## D. Code Review Detection Patterns

| Issue | Pattern to Detect | Severity |
|-------|-------------------|----------|
| **Service 拋例外** | Service 層使用 `throw` 處理商業邏輯 | 🔴 Critical |
| **new Failure()** | 直接建立 `new Failure()` 而非使用 FailureProvider | 🔴 Critical |
| **未檢查 Result** | 直接使用 `.Value` 未檢查 `.IsFailure` | 🔴 Critical |
| **Empty Catch** | `catch { }` 或 `catch (Exception) { }` 不處理 | 🔴 Critical |
| **throw ex** | `throw ex;` 而非 `throw;`（遺失堆疊追蹤） | 🟡 Bug |
| **Controller 拋例外** | Controller 使用 `throw` 而非 `this.Failure()` | 🟡 Warning |

---

## E. ErrorCode 常用值

```csharp
// 常用 ErrorCode（定義於 JobBank1111.Event.Utility.ErrorHandling.ErrorCode）
ErrorCode.Unauthorized      // 401 未授權
ErrorCode.Forbidden         // 403 禁止存取
ErrorCode.NotFound          // 404 找不到資源
ErrorCode.ValidationError   // 400 驗證錯誤
ErrorCode.DatabaseError     // 500 資料庫錯誤
ErrorCode.ExternalServiceError // 502 外部服務錯誤
ErrorCode.UnknownError      // 500 未知錯誤
```
