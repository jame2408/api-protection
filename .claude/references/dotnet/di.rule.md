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
// ✅ 本專案 Service / Handler 一律使用 Primary Constructor
// ✅ Service 與 Handler 不注入 ILogger（CLAUDE.md NEVER 規則）
public class CreateApiKeyHandler(
    IConsumerValidator consumerValidator,
    IApiKeyRepository keyRepository,
    IScopeRegistry scopeRegistry,
    IAccessPolicyService accessPolicyService) : ICreateApiKeyHandler
{
    // HandleAsync 主體略；Result-based 流程詳見 exceptions.rule.md。
}
```

### Repository DbContext 注入

```csharp
// ✅ Scoped Repository 直接注入 Scoped DbContext（官方推薦，本專案標準）
//    DbContext 透過 AddDbContextPool 註冊，效能對齊高流量需求；詳見 ef-core.rule.md。
public class ApiKeyRepository(AppDbContext db) : IApiKeyRepository
{
    public async Task<int> CountActiveAsync(
        string consumerId, string environment, string tenantId, CancellationToken cancel = default)
    {
        return await db.ApiKeys.CountAsync(k =>
            k.ConsumerId == consumerId &&
            k.Environment == environment &&
            k.TenantId == tenantId &&
            k.Status == ApiKeyStatus.Active, cancel);
    }
}

// ❌ 一般 Scoped Repository 不應使用 IDbContextFactory
public class ApiKeyRepository(IDbContextFactory<AppDbContext> contextFactory) // ❌
```

> ⚠️ **`IDbContextFactory` 使用場景**：僅限 Singleton、Background Service (`IHostedService`)、
> 或 Blazor 等需要顯式控制 DbContext 生命週期的情境。一般 Scoped Repository **不需要**
> Factory，請直接注入 `AppDbContext`。

### 環境變數注入

```csharp
// ✅ 環境變數使用 record 繼承 EnvironmentVariable
public record SYS_REDIS_URL : EnvironmentVariable;

// 註冊
services.AddSysEnvironments(); // 在 Host/Program.cs 或對應 Module 註冊

// 注入使用
public class CacheService(SYS_REDIS_URL redisUrl)
{
    // ADR-005 §3 settings snapshot exception: cache the env var value once at construction.
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

每個 Bounded Context 自帶 `*Module.cs`，集中該 BC 的 DI + endpoint 註冊；
跨 BC 的基礎設施（DbContext、Repositories）集中於 `Infrastructure/InfrastructureModule.cs`。
`Host/Program.cs` 只串接這些 module，不直接註冊個別服務。

```csharp
// ✅ Infrastructure/InfrastructureModule.cs — 跨 BC 的持久層
public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextPool<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IAccessPolicyRepository, AccessPolicyRepository>();
        services.AddScoped<IScopeRegistry, ScopeRegistryService>();
        return services;
    }
}

// ✅ KeyLifecycle/KeyLifecycleModule.cs — BC 內部 Handlers + endpoint mapping
public static class KeyLifecycleModule
{
    public static IServiceCollection AddKeyLifecycleModule(this IServiceCollection services)
    {
        services.AddScoped<ICreateApiKeyHandler, CreateApiKeyHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapKeyLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        CreateApiKeyEndpoint.Map(app);
        return app;
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
| **Scoped Repository 誤用 Factory** | 一般 Scoped Repository 注入 `IDbContextFactory<>` 而非 `AppDbContext`（Singleton/Background/Blazor 例外） | 🟡 Warning |
| **Service/Handler 注入 ILogger** | `Service` / `Handler` constructor 注入 `ILogger<T>`（觀測性應在 Endpoint/Middleware/Pipeline Behavior 等邊界） | 🔴 Critical |
