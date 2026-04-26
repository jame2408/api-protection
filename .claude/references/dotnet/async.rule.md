# .NET Async/Await 規則

非同步程式設計的最佳實踐。

---

## A. 本專案 Async 規範

### CancellationToken 命名

```csharp
// ✅ 本專案使用 cancel 作為參數名，並給預設值
public async Task<Result<Order, Failure>> GetOrderAsync(int id, CancellationToken cancel = default)
{
    return await _repository.GetByIdAsync(id, cancel);
}

// ❌ 不使用 cancellationToken（太長）
public async Task<Order> GetOrderAsync(int id, CancellationToken cancellationToken = default) // ❌
```

### 搭配 Result Pattern

```csharp
// ✅ 本專案 Service 層標準寫法
public async Task<Result<OrderResponse, Failure>> GetOrderAsync(
    int orderId, 
    CancellationToken cancel = default)
{
    var order = await _repository.GetByIdAsync(orderId, cancel);
    if (order is null)
    {
        return FailureProvider.CreateFailure(ErrorCode.NotFound);
    }
    
    return new OrderResponse { Id = order.Id, Total = order.Total };
}
```

---

## B. Do's ✅

### Async All The Way

```csharp
// ✅ 從 Controller 到 Repository 全程 async
[HttpGet("{id}")]
public async Task<IActionResult> GetOrder(int id, CancellationToken cancel = default)
{
    var result = await _orderService.GetOrderAsync(id, cancel);
    if (result.IsFailure)
    {
        return this.Failure(result.Error);
    }
    return Ok(result.Value);
}
```

### Task.WhenAll 並行執行

```csharp
// ✅ 並行執行多個獨立操作
public async Task<Result<OrderSummary, Failure>> GetOrderSummaryAsync(
    int orderId, 
    CancellationToken cancel = default)
{
    var orderTask = _orderRepository.GetByIdAsync(orderId, cancel);
    var itemsTask = _itemRepository.GetByOrderIdAsync(orderId, cancel);
    var customerTask = _customerRepository.GetByOrderIdAsync(orderId, cancel);
    
    await Task.WhenAll(orderTask, itemsTask, customerTask);
    
    return new OrderSummary
    {
        Order = orderTask.Result,
        Items = itemsTask.Result,
        Customer = customerTask.Result,
    };
}
```

### ConfigureAwait(false) in Libraries

```csharp
// ✅ 共用 Library 中使用（例如 SharedKernel 內的非 ASP.NET 程式碼）
//    Endpoint/Handler 中不需要 ConfigureAwait(false)，ASP.NET Core 沒有 sync context。
public async Task<string> ProcessDataAsync(string input, CancellationToken cancel = default)
{
    var result = await _externalService.CallAsync(input, cancel).ConfigureAwait(false);
    return result;
}
```

---

## C. Don'ts ❌

### 禁止 .Result / .Wait()

```csharp
// ❌ DEADLOCK 風險!
var order = GetOrderAsync(id).Result;
GetOrderAsync(id).Wait();
var order = GetOrderAsync(id).GetAwaiter().GetResult();

// ✅ 使用 await
var order = await GetOrderAsync(id, cancel);
```

**為什麼會 Deadlock？**
1. ASP.NET 有 synchronization context
2. `.Result` 阻塞執行緒等待完成
3. async 的 continuation 需要同一條執行緒
4. 執行緒被阻塞 → continuation 無法執行 → Deadlock

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
        return FailureProvider.CreateFailure(ErrorCode.Timeout, "操作逾時");
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
        _logger.LogError(ex, "Failed to send email for order {OrderId}", order.Id);
    }
});
```

---

## E. Code Review Detection Patterns

| Pattern | Issue | Severity |
|---------|-------|----------|
| `.Result` | Async Deadlock | 🔴 Critical |
| `.Wait()` | Async Deadlock | 🔴 Critical |
| `.GetAwaiter().GetResult()` | Async Deadlock | 🔴 Critical |
| `async void` | Unobserved exceptions | 🔴 Critical |
| `await` in `foreach` | Sequential when parallelizable | 🟡 Performance |
| Missing `CancellationToken` | Can't cancel long operations | 🟡 Warning |
| `cancellationToken` 參數名 | 本專案使用 `cancel` | 🟢 Style |
