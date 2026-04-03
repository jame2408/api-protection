# EF Core 規則

Entity Framework Core 模式、反模式與效能優化。

---

## A. 本專案 EF Core 規範

### DbContextFactory 模式

```csharp
// ✅ 本專案使用 DbContextFactory
public class OrderRepository(
    IDbContextFactory<EventDbContext> contextFactory,
    ILogger<OrderRepository> logger)
{
    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancel = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancel);
        return await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancel);
    }
}

// ❌ 禁止直接注入 DbContext
public class OrderRepository(EventDbContext context) // ❌
{
}
```

### 搭配 Result Pattern

```csharp
// ✅ Repository 回傳 Result
public async Task<Result<Order, Failure>> GetByIdAsync(int id, CancellationToken cancel = default)
{
    try
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancel);
        var order = await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancel);
        
        if (order is null)
        {
            return FailureProvider.CreateFailure(ErrorCode.NotFound, "訂單不存在");
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

---

## B. Query Optimization

### Use Projection (Select)

```csharp
// ❌ Over-fetching - 載入所有欄位
var orders = await context.Orders
    .Where(o => o.CustomerId == customerId)
    .ToListAsync(cancel);

// ✅ Projection - 只載入需要的欄位
var orderSummaries = await context.Orders
    .Where(o => o.CustomerId == customerId)
    .Select(o => new OrderSummary
    {
        Id = o.Id,
        Total = o.Total,
        ItemCount = o.Items.Count,
    })
    .ToListAsync(cancel);
```

### Use AsNoTracking for Read-Only Queries

```csharp
// ❌ 不必要的 tracking 開銷
var orders = await context.Orders
    .Where(o => o.Status == OrderStatus.Active)
    .ToListAsync(cancel);

// ✅ 唯讀查詢使用 AsNoTracking
var orders = await context.Orders
    .AsNoTracking()
    .Where(o => o.Status == OrderStatus.Active)
    .ToListAsync(cancel);
```

### Use Include for Eager Loading

```csharp
// ❌ N+1 Query Problem
var orders = await context.Orders.ToListAsync(cancel);
foreach (var order in orders)
{
    // 每次迭代都觸發新的查詢!
    Console.WriteLine(order.Customer.Name);
    Console.WriteLine(order.Items.Count);
}

// ✅ Eager Loading with Include
var orders = await context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
    .ToListAsync(cancel);
```

### Use AsSplitQuery for Multiple Collections

```csharp
// ❌ Cartesian Explosion - CROSS JOIN 產生大量重複資料
var orders = await context.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .Include(o => o.ShippingDetails)
    .ToListAsync(cancel);

// ✅ Split Query - 多個有效率的查詢
var orders = await context.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .Include(o => o.ShippingDetails)
    .AsSplitQuery()
    .ToListAsync(cancel);
```

---

## C. Common Anti-Patterns

### N+1 Query Problem

| Detection | Impact | Fix |
|-----------|--------|-----|
| 迴圈內存取 navigation property | N+1 次資料庫查詢 | Use `.Include()` |
| Lazy loading 在迴圈內觸發 | 隱藏的 N+1 查詢 | Use eager loading |

```csharp
// ❌ N+1 Problem (1 + N queries)
var customers = await context.Customers.ToListAsync(cancel);
foreach (var customer in customers)
{
    var orderCount = customer.Orders.Count; // 觸發 lazy load!
}

// ✅ Single query with projection
var customerOrderCounts = await context.Customers
    .Select(c => new { c.Name, OrderCount = c.Orders.Count })
    .ToListAsync(cancel);
```

### In-Memory Evaluation

```csharp
// ❌ Client-side evaluation (先載入所有資料再過濾)
var orders = await context.Orders
    .Where(o => MyCustomMethod(o.Data)) // 無法轉換為 SQL
    .ToListAsync(cancel);

// ✅ 先在資料庫過濾，再套用自訂邏輯
var orders = await context.Orders
    .Where(o => o.Status == OrderStatus.Active) // SQL 可轉換
    .ToListAsync(cancel);
var filtered = orders.Where(o => MyCustomMethod(o.Data));
```

---

## D. Transaction Patterns

### Implicit Transaction (SaveChanges)

```csharp
// ✅ SaveChanges 自動包裝在 transaction 中
await using var context = await _contextFactory.CreateDbContextAsync(cancel);

var order = new Order { /* ... */ };
context.Orders.Add(order);

var payment = new Payment { OrderId = order.Id };
context.Payments.Add(payment);

await context.SaveChangesAsync(cancel); // 單一 transaction
```

### Explicit Transaction

```csharp
// ✅ 跨多個 SaveChanges 的操作
await using var context = await _contextFactory.CreateDbContextAsync(cancel);
await using var transaction = await context.Database.BeginTransactionAsync(cancel);

try
{
    await context.SaveChangesAsync(cancel);
    
    // 外部操作
    await _externalService.NotifyAsync(cancel);
    
    await context.SaveChangesAsync(cancel);
    await transaction.CommitAsync(cancel);
}
catch
{
    await transaction.RollbackAsync(cancel);
    throw;
}
```

---

## E. Code Review Detection Patterns

| Issue | Pattern to Detect | Severity |
|-------|-------------------|----------|
| **N+1 Query** | 迴圈內存取 navigation property | 🟡 Performance |
| **Cartesian Explosion** | 多個 `.Include()` 沒有 `.AsSplitQuery()` | 🟡 Performance |
| **Over-fetching** | `ToListAsync()` 沒有 `.Select()` projection | 🟢 Consider |
| **Tracking Overhead** | 唯讀查詢沒有 `.AsNoTracking()` | 🟢 Consider |
| **In-Memory Evaluation** | Where 內使用無法轉換的 C# 方法 | 🟡 Performance |
| **直接注入 DbContext** | 注入 DbContext 而非 DbContextFactory | 🟡 Warning |
| **缺少 CancellationToken** | Async 方法沒有傳遞 cancel 參數 | 🟢 Consider |
