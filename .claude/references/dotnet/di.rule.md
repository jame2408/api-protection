# .NET Dependency Injection 規則

DI 模式、生命週期與常見陷阱。

---

## A. Service Lifetimes

| Lifetime | Description | Use Case |
|----------|-------------|----------|
| **Singleton** | 應用程式生命週期內單一實例 | Caches, Configuration, HttpClient |
| **Scoped** | 每個 Request 一個實例 | DbContext, Repositories, Services |
| **Transient** | 每次注入都建立新實例 | 輕量、無狀態的服務 |

```csharp
// 註冊範例
services.AddSingleton<ICacheProvider, RedisCacheProvider>();
services.AddScoped<IOrderRepository, OrderRepository>();
services.AddTransient<IEmailSender, SmtpEmailSender>();
```

---

## B. 本專案常用模式

### Primary Constructor（.NET 8+）

```csharp
// ✅ 本專案使用 Primary Constructor
public class OrderService(
    IOrderRepository repository,
    IFailureProvider failureProvider,
    ILogger<OrderService> logger)
{
    public async Task<Result<Order, Failure>> GetOrderAsync(int id, CancellationToken cancel)
    {
        var order = await repository.GetByIdAsync(id, cancel);
        if (order is null)
        {
            return failureProvider.CreateFailure(ErrorCode.NotFound);
        }
        return order;
    }
}
```

### DbContextFactory 模式

```csharp
// ✅ 本專案使用 DbContextFactory 而非直接注入 DbContext
public class OrderRepository(
    IDbContextFactory<EventDbContext> contextFactory,
    ILogger<OrderRepository> logger)
{
    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancel)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancel);
        return await context.Orders.FirstOrDefaultAsync(o => o.Id == id, cancel);
    }
}
```

### 環境變數注入

```csharp
// ✅ 環境變數使用 record 繼承 EnvironmentVariable
public record SYS_REDIS_URL : EnvironmentVariable;

// 註冊
services.AddSysEnvironments(); // 在 ServiceCollectionExtensions.cs

// 注入使用
public class CacheService(SYS_REDIS_URL redisUrl)
{
    private readonly string _connectionString = redisUrl.Value;
}
```

---

## C. Middleware DI 模式

### 本專案 Middleware 寫法

```csharp
// ✅ 本專案 Middleware 使用 Primary Constructor + IServiceProvider
public class CookieValidationMiddleware(
    ASPNETCORE_ENVIRONMENT environment,
    RequestDelegate next,
    IServiceProvider serviceProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 建立 Scope 解析 Scoped 服務
        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        
        // 使用 repository...
        
        await next(context);
    }
}

// ❌ 禁止直接在 Constructor 注入 Scoped 服務
public class BadMiddleware(
    RequestDelegate next,
    IAccountRepository repository) // ❌ Scoped 服務被 Singleton Middleware 捕獲!
{
}
```

### 為什麼需要 CreateScope？

Middleware 是 Singleton，直接注入 Scoped 服務會造成「Captive Dependency」：
- Scoped 服務變成實質上的 Singleton
- 可能導致資料混亂（跨 Request 共用同一個 DbContext）

---

## D. Anti-Patterns（Critical）

### Captive Dependency

```csharp
// ❌ CRITICAL - Singleton 捕獲 Scoped 服務
public class MySingleton // 註冊為 Singleton
{
    private readonly IScopedService _scopedService; // Scoped!
    
    public MySingleton(IScopedService scopedService)
    {
        _scopedService = scopedService; // 永遠是同一個實例!
    }
}

// ✅ 使用 IServiceProvider.CreateScope()
public class MySingleton(IServiceProvider serviceProvider)
{
    public async Task DoWorkAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var scopedService = scope.ServiceProvider.GetRequiredService<IScopedService>();
        await scopedService.ProcessAsync();
    }
}
```

### Socket Exhaustion (HttpClient)

```csharp
// ❌ CRITICAL - 每次都建立新的 HttpClient
public class MyService
{
    public async Task<string> CallApiAsync()
    {
        using var client = new HttpClient(); // 每次都建立新的 Socket!
        return await client.GetStringAsync("https://api.example.com");
    }
}

// ✅ 使用 IHttpClientFactory
public class MyService(IHttpClientFactory httpClientFactory)
{
    public async Task<string> CallApiAsync()
    {
        var client = httpClientFactory.CreateClient();
        return await client.GetStringAsync("https://api.example.com");
    }
}

// ✅ 或使用 Named Client
services.AddHttpClient("ExternalApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

### Transient Disposable

```csharp
// ❌ 記憶體洩漏 - Transient IDisposable
services.AddTransient<IMyService, MyDisposableService>(); // 實作 IDisposable

// ✅ Disposable 服務使用 Scoped
services.AddScoped<IMyService, MyDisposableService>();
```

---

## E. 本專案服務註冊位置

服務註冊集中於 `ServiceCollectionExtensions.cs`：

```csharp
// JobBank1111.Event.WebAPI.ServiceExtensions.ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 註冊 Services
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
    
    public static IServiceCollection AddApplicationRepositories(this IServiceCollection services)
    {
        // 註冊 Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }
}
```

---

## F. Code Review Detection Patterns

| Issue | Pattern to Detect | Severity |
|-------|-------------------|----------|
| **Captive Dependency** | Singleton constructor 注入 Scoped 服務 | 🔴 Critical |
| **Socket Exhaustion** | 方法內 `new HttpClient()` | 🔴 Critical |
| **Middleware Scoped 注入** | Middleware constructor 注入 Scoped 服務 | 🔴 Critical |
| **Transient Disposable** | `AddTransient` + `IDisposable` | 🟡 Memory Leak |
| **Service Locator** | Constructor 內呼叫 `GetService` | 🟢 Code Smell |
| **直接注入 DbContext** | 注入 DbContext 而非 DbContextFactory | 🟡 Warning |
