# .NET Async/Await 規則

非同步程式設計的最佳實踐。

---

## A. 本專案 Async 規範

### CancellationToken 命名

```csharp
// ✅ 本專案使用 cancel 作為參數名，並給預設值
public class GetOrderHandler(IOrderRepository orderRepository)
{
    public async Task<Result<OrderResponse, Failure>> HandleAsync(
        int id,
        CancellationToken cancel = default)
    {
        var order = await orderRepository.GetByIdAsync(id, cancel);
        if (order is null)
            return FailureProvider.CreateFailure(GetOrderFailureCodes.OrderNotFound);

        return new OrderResponse { Id = order.Id, Total = order.Total };
    }
}

// ❌ 不使用 cancellationToken（太長）
public async Task<Order> GetOrderAsync(int id, CancellationToken cancellationToken = default) // ❌
```

### 搭配 Result Pattern

```csharp
// ✅ 本專案 Handler / Service 標準寫法
public class GetOrderHandler(IOrderRepository orderRepository)
{
    public async Task<Result<OrderResponse, Failure>> HandleAsync(
        int orderId,
        CancellationToken cancel = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancel);
        if (order is null)
        {
            return FailureProvider.CreateFailure(GetOrderFailureCodes.OrderNotFound);
        }

        return new OrderResponse { Id = order.Id, Total = order.Total };
    }
}
```

---

## B. Do's ✅

### Async All The Way

```csharp
// ✅ 從 Minimal API endpoint 到 Handler / Repository 全程 async
app.MapGet("/orders/{id}", async (
    int id,
    IGetOrderHandler handler,
    CancellationToken cancel) =>
{
    var result = await handler.HandleAsync(id, cancel);
    if (result.IsFailure)
    {
        return Results.NotFound(new { error = result.Error.Code });
    }

    return Results.Ok(result.Value);
});
```

### Task.WhenAll 並行執行

```csharp
// ✅ 並行執行多個獨立操作
public class GetOrderSummaryHandler(
    IOrderRepository orderRepository,
    IOrderItemRepository itemRepository,
    ICustomerRepository customerRepository)
{
    public async Task<Result<OrderSummary, Failure>> HandleAsync(
        int orderId,
        CancellationToken cancel = default)
    {
        var orderTask = orderRepository.GetByIdAsync(orderId, cancel);
        var itemsTask = itemRepository.GetByOrderIdAsync(orderId, cancel);
        var customerTask = customerRepository.GetByOrderIdAsync(orderId, cancel);

        await Task.WhenAll(orderTask, itemsTask, customerTask);

        return new OrderSummary
        {
            Order = orderTask.Result,
            Items = itemsTask.Result,
            Customer = customerTask.Result,
        };
    }
}
```

### ConfigureAwait(false) in Libraries

```csharp
// ✅ 共用 Library 中使用（例如 SharedKernel 內的非 ASP.NET 程式碼）
//    Endpoint/Handler 中不需要 ConfigureAwait(false)，ASP.NET Core 沒有 sync context。
public class ExternalDataProcessor(IExternalService externalService)
{
    public async Task<string> ProcessDataAsync(string input, CancellationToken cancel = default)
    {
        var result = await externalService.CallAsync(input, cancel).ConfigureAwait(false);
        return result;
    }
}
```

---

## C. Don'ts ❌

### 禁止 .Result / .Wait()

```csharp
// ❌ Blocking async call 會浪費 thread pool，並可能造成 deadlock / starvation
var order = GetOrderAsync(id).Result;
GetOrderAsync(id).Wait();
var order = GetOrderAsync(id).GetAwaiter().GetResult();

// ✅ 使用 await
var order = await GetOrderAsync(id, cancel);
```

**為什麼要避免 blocking async？**
1. `.Result` / `.Wait()` 會阻塞目前 thread 等待 async work 完成
2. 高併發下容易造成 thread pool starvation
3. 在有 synchronization context 的環境中可能造成 deadlock
4. 錯誤處理也會變差，例外常被包成 `AggregateException`

### 禁止 async void

```csharp
// ❌ 例外會遺失，無法 await
public async void ProcessOrder(Order order)
{
    await SaveAsync(order); // 如果拋出例外，應用程式可能崩潰
}

// ✅ 回傳 Task
public async Task ProcessOrderAsync(Order order, CancellationToken cancel = default)
{
    await SaveAsync(order, cancel);
}
```

### 避免迴圈內 await（可並行時）

```csharp
// ❌ 循序執行 - 慢!
foreach (var id in orderIds)
{
    var order = await GetOrderAsync(id, cancel);
    results.Add(order);
}

// ✅ 並行執行 - 快!
var tasks = orderIds.Select(id => GetOrderAsync(id, cancel));
var orders = await Task.WhenAll(tasks);
```

---

## D. 常見模式

### Timeout 模式

```csharp
public async Task<Result<Order, Failure>> GetOrderWithTimeoutAsync(
    int id,
    CancellationToken cancel = default)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
    cts.CancelAfter(TimeSpan.FromSeconds(30));

    try
    {
        return await GetOrderAsync(id, cts.Token);
    }
    catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
    {
        return FailureProvider.CreateFailure(GetOrderFailureCodes.Timeout);
    }
}
```

### Safe Fire and Forget

> ⚠️ Fire-and-forget 屬於邊界行為，本專案應放在 Background Service / Hosted Service /
> Infrastructure 層（這些層才允許持有 `ILogger`）。Service / Handler 不應直接 spawn
> fire-and-forget Task — 改為發 domain event，讓 background processor 處理。

```csharp
// ❌ 危險 - 例外遺失
_ = SendEmailAsync(order);

// ✅ 安全的 Fire and Forget（位於 Hosted Service / Background Service 等邊界）
_ = Task.Run(async () =>
{
    try
    {
        await SendEmailAsync(order, CancellationToken.None);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send email for order {OrderId}", order.Id);
    }
});
```

---

## E. Code Review Detection Patterns

| Pattern | Issue | Severity |
|---------|-------|----------|
| `.Result` | Async Deadlock / Thread Pool Starvation | 🔴 Critical |
| `.Wait()` | Async Deadlock / Thread Pool Starvation | 🔴 Critical |
| `.GetAwaiter().GetResult()` | Async Deadlock / Thread Pool Starvation | 🔴 Critical |
| `async void` | Unobserved exceptions | 🔴 Critical |
| `await` in `foreach` | Sequential when parallelizable | 🟡 Performance |
| Missing `CancellationToken` | Can't cancel long operations | 🟡 Warning |
| `cancellationToken` 參數名 | 本專案使用 `cancel` | 🟢 Style |
