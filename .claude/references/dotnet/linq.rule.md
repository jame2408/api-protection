# LINQ 規則

LINQ 效能模式與常見陷阱。

---

## A. Performance Patterns

### Any() vs Count()

```csharp
// ❌ 低效 - 計算所有元素
if (orders.Count() > 0)
{
    // ...
}

// ✅ 高效 - 找到第一個就停止
if (orders.Any())
{
    // ...
}

// ✅ 搭配 predicate
if (orders.Any(o => o.Status == OrderStatus.Pending))
{
    // ...
}
```

### FirstOrDefault with Predicate

```csharp
// ❌ 較低效 - 建立中間 enumerable
var order = orders.Where(o => o.Id == id).FirstOrDefault();

// ✅ 更高效 - 單次遍歷
var order = orders.FirstOrDefault(o => o.Id == id);
```

### Dictionary Lookup vs FirstOrDefault in Loop

```csharp
// ❌ O(n²) - 迴圈內 FirstOrDefault
foreach (var orderId in orderIds)
{
    var order = allOrders.FirstOrDefault(o => o.Id == orderId);
    if (order != null)
        Process(order);
}

// ✅ O(n) - Dictionary lookup
var orderDict = allOrders.ToDictionary(o => o.Id);
foreach (var orderId in orderIds)
{
    if (orderDict.TryGetValue(orderId, out var order))
        Process(order);
}
```

### Avoid Multiple Enumeration

```csharp
// ❌ 列舉兩次
IEnumerable<Order> orders = GetOrders();
var count = orders.Count();     // 第一次列舉
var total = orders.Sum(o => o.Amount); // 第二次列舉

// ✅ 一次具體化
var orderList = GetOrders().ToList();
var count = orderList.Count;    // 不需列舉
var total = orderList.Sum(o => o.Amount); // 單次列舉
```

### Premature Materialization

```csharp
// ❌ 過早具體化 - 載入所有資料
var orders = _context.Orders.ToList()
    .Where(o => o.Status == OrderStatus.Active)
    .Take(10);

// ✅ 先在資料庫過濾
await using var context = await _contextFactory.CreateDbContextAsync(cancel);
var orders = await context.Orders
    .Where(o => o.Status == OrderStatus.Active)
    .Take(10)
    .ToListAsync(cancel);
```

---

## B. Readability Patterns

### Method Chain Formatting

```csharp
// ✅ 複雜查詢每行一個操作
var result = await context.Orders
    .AsNoTracking()
    .Where(o => o.Status == OrderStatus.Pending)
    .Where(o => o.CreatedAt > DateTime.UtcNow.AddDays(-7))
    .OrderByDescending(o => o.Priority)
    .ThenBy(o => o.CreatedAt)
    .Select(o => new OrderSummary
    {
        Id = o.Id,
        CustomerName = o.CustomerName,
        Total = o.Total,
    })
    .ToListAsync(cancel);
```

### Query Syntax for Joins

```csharp
// ✅ Join 使用 Query Syntax 更清楚
var result = 
    from order in context.Orders
    join customer in context.Customers on order.CustomerId equals customer.Id
    where order.Status == OrderStatus.Active
    select new { order, customer };

// Method syntax 等效寫法（Join 時可讀性較低）
var result = context.Orders
    .Join(context.Customers,
        order => order.CustomerId,
        customer => customer.Id,
        (order, customer) => new { order, customer })
    .Where(x => x.order.Status == OrderStatus.Active);
```

---

## C. Common Pitfalls

### Null Reference After FirstOrDefault

```csharp
// ❌ NullReferenceException 風險
var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == id, cancel);
var customerName = order.Customer.Name; // 如果 order 為 null 會拋出例外!

// ✅ 本專案使用 Result Pattern 檢查
var order = await context.Orders
    .AsNoTracking()
    .FirstOrDefaultAsync(o => o.Id == id, cancel);

if (order is null)
{
    return FailureProvider.CreateFailure(GetOrderFailureCodes.OrderNotFound);
}

var customerName = order.Customer?.Name ?? "Unknown";

// ✅ 或使用 pattern matching
if (await context.Orders.FirstOrDefaultAsync(o => o.Id == id, cancel) is { } order)
{
    var customerName = order.Customer.Name;
}
```

### SelectMany Flatten

```csharp
// 取得所有訂單的所有項目
// ❌ 巢狀迴圈
var allItems = new List<OrderItem>();
foreach (var order in orders)
{
    allItems.AddRange(order.Items);
}

// ✅ SelectMany
var allItems = orders.SelectMany(o => o.Items).ToList();
```

### GroupBy Performance

```csharp
// ❌ 載入所有資料後在記憶體內 GroupBy
await using var context = await _contextFactory.CreateDbContextAsync(cancel);
var ordersByCustomer = (await context.Orders.ToListAsync(cancel))
    .GroupBy(o => o.CustomerId);

// ✅ 在資料庫內 GroupBy
var ordersByCustomer = await context.Orders
    .GroupBy(o => o.CustomerId)
    .Select(g => new 
    { 
        CustomerId = g.Key, 
        OrderCount = g.Count(),
        TotalAmount = g.Sum(o => o.Amount),
    })
    .ToListAsync(cancel);
```

---

## D. Code Review Detection Patterns

| Issue | Pattern to Detect | Severity |
|-------|-------------------|----------|
| **Count vs Any** | `.Count() > 0` 或 `.Count() == 0` | 🟢 Performance |
| **O(n²) Lookup** | 迴圈內 `.FirstOrDefault()` | 🟡 Performance |
| **Multiple Enumeration** | `IEnumerable` 變數使用兩次 | 🟡 Performance |
| **Premature Materialization** | `.ToList()` 在 `.Where()` 或 `.Take()` 之前 | 🟡 Performance |
| **Null Reference** | `FirstOrDefault` 後沒有 null 檢查 | 🔴 Bug |
| **In-Memory GroupBy** | `.ToList().GroupBy()` | 🟡 Performance |
| **缺少 CancellationToken** | `ToListAsync()` 沒有傳 cancel | 🟢 Consider |
