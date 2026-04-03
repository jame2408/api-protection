# .NET 命名規範

本專案的命名規範與 C# 編碼標準。

---

## A. 本專案命名規範

### 層級命名

| 層級 | 命名規則 | 範例 |
|------|----------|------|
| **Controller** | `[Name]Controller` | `OrderController`, `TransferController` |
| **Service** | `[Name]Service` + `I[Name]Service` | `OrderService`, `IOrderService` |
| **Repository** | `[Name]Repository` + `I[Name]Repository` | `OrderRepository`, `IOrderRepository` |
| **ApiClient** | `[Name]ApiClient` + `I[Name]ApiClient` | `ExternalApiClient`, `IExternalApiClient` |

> ⚠️ **注意**：外部 API 呼叫使用 `*ApiClient`，不是 `*Client`

### 專案結構

```
src/be/
├── JobBank1111.Event.WebAPI/           # Presentation Layer
│   ├── Controllers/
│   │   └── [Domain]/
│   │       └── [Name]Controller.cs
│   └── Middlewares/
│       └── [Name]Middleware.cs
├── JobBank1111.Event.Service/          # Business Logic Layer
│   ├── Events/
│   │   └── [Domain]/
│   │       ├── Services/
│   │       │   └── [Name]Service.cs
│   │       ├── Interfaces/
│   │       │   └── I[Name]Service.cs
│   │       └── Models/
│   │           ├── Request/
│   │           ├── Response/
│   │           └── Dto/
│   └── Common/
│       └── [SharedDomain]/
└── JobBank1111.Event.DataAccess/       # Data Access Layer
    └── Events/
        └── [Domain]/
            ├── [Name]Repository.cs
            └── [Name]ApiClient.cs
```

---

## B. Microsoft 標準命名

| Element | Convention | Example |
|---------|------------|---------|
| Namespace | PascalCase | `JobBank1111.Event.Service` |
| Class | PascalCase, noun | `OrderService`, `CustomerRepository` |
| Interface | IPascalCase | `IOrderRepository`, `IDisposable` |
| Method | PascalCase, verb | `GetOrderById`, `ValidateInput` |
| Property | PascalCase | `FirstName`, `IsEnabled` |
| Field (private) | _camelCase | `_orderRepository`, `_logger` |
| Field (const) | PascalCase | `MaxRetryCount`, `DefaultTimeout` |
| Parameter | camelCase | `orderId`, `customerName` |
| Local variable | camelCase | `orderCount`, `isValid` |
| Enum | PascalCase (singular) | `OrderStatus`, `PaymentType` |

### Async 方法命名

```csharp
// ✅ Async 方法以 Async 結尾
public async Task<Order> GetOrderAsync(int id, CancellationToken cancel);
public async Task SaveOrderAsync(Order order, CancellationToken cancel);

// ✅ 本專案要求傳入 CancellationToken
public async Task<Result<Order, Failure>> GetOrderAsync(int id, CancellationToken cancel);
```

### Boolean 命名

```csharp
// ✅ 使用 is/has/can/should 前綴
public bool IsEnabled { get; set; }
public bool HasPermission { get; set; }
public bool CanEdit { get; set; }

// Methods returning bool
public bool IsValid();
public bool HasAccess(User user);
```

---

## C. 本專案 C# 風格

### File-Scoped Namespaces ✅

```csharp
// ✅ 本專案使用 file-scoped namespace
namespace JobBank1111.Event.Service.Events.Order;

public class OrderService
{
}
```

### Primary Constructors ✅

```csharp
// ✅ 本專案使用 Primary Constructor (.NET 8+)
public class OrderService(
    IOrderRepository repository,
    ILogger<OrderService> logger)
{
    public async Task<Result<Order, Failure>> GetOrderAsync(int id, CancellationToken cancel)
    {
        logger.LogInformation("Getting order {OrderId}", id);
        return await repository.GetByIdAsync(id, cancel);
    }
}
```

### Collection 初始化

```csharp
// ✅ 本專案使用傳統語法
var list = new List<int> { 1, 2, 3 };
var array = new[] { "a", "b", "c" };
var dict = new Dictionary<string, int> { ["key"] = 1 };

// ⚠️ 本專案尚未採用 Collection Expressions
// List<int> list = [1, 2, 3]; // 暫不使用
```

---

## D. 註解規範

### XML 文件

```csharp
/// <summary>
/// 取得訂單資訊。
/// </summary>
/// <param name="orderId">訂單編號。</param>
/// <param name="cancel">取消權杖。</param>
/// <returns>訂單資訊，若找不到則回傳 Failure。</returns>
public async Task<Result<OrderResponse, Failure>> GetOrderAsync(
    int orderId, 
    CancellationToken cancel)
{
}
```

### 何時該寫註解

✅ **應該寫註解：**
- 解釋「為什麼」而非「做什麼」
- 警告特殊行為或後果
- TODO/FIXME 搭配票號

❌ **不該寫註解：**
- 解釋顯而易見的程式碼
- 註解掉舊程式碼（用版本控制）

```csharp
// ❌ BAD: 解釋顯而易見的事
// 取得訂單
var order = await GetOrderAsync(orderId);

// ✅ GOOD: 解釋為什麼
// 使用迴圈而非 LINQ，效能測試顯示在 10k+ 筆時快 3 倍
foreach (var order in orders)
{
    total += order.Amount;
}
```

---

## E. API 路由命名

```csharp
// ✅ 本專案 API 路由規範
[ApiController]
[Route("api/v1/[controller]")]  // lowerCamelCase
public class OrderController : SystemController
{
    [HttpGet("{id}")]           // RESTful
    public async Task<IActionResult> GetOrder(int id) { }
    
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request) { }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrder(int id, [FromBody] UpdateOrderRequest request) { }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(int id) { }
}
```

---

## F. 環境變數命名

```csharp
// ✅ 環境變數使用 UPPER_SNAKE_CASE
public record SYS_REDIS_URL : EnvironmentVariable;
public record PG_EVENT_CONNECTION_STRING : EnvironmentVariable;
public record ASPNETCORE_ENVIRONMENT : EnvironmentVariable;

// 使用方式
public class MyService(SYS_REDIS_URL redisUrl)
{
    private readonly string _connectionString = redisUrl.Value;
}
```

---

## G. 檔案組織

### 一個類別一個檔案

```
// 正確
OrderService.cs       → public class OrderService
IOrderService.cs      → public interface IOrderService
OrderStatus.cs        → public enum OrderStatus

// 例外：相關的小型類別可以放在一起
OrderModels.cs        → OrderRequest, OrderResponse (相關 DTO)
```

### Request/Response 命名

```csharp
// Request: [Action][Domain]Request
public class CreateOrderRequest { }
public class UpdateOrderRequest { }
public class GetOrdersRequest { }

// Response: [Action][Domain]Response 或 [Domain]Response
public class GetOrderResponse { }
public class GetOrdersResponse { }
public class CreateOrderResponse { }
```
