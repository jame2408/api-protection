# .NET 命名規範

本專案的命名規範與 C# 編碼標準。

---

## A. 本專案命名規範

### 層級命名

本專案採用 **Vertical Slice + Bounded Context** 架構，HTTP 端點以 Minimal API 表示，
不使用 MVC Controller。每個 use case 是一個資料夾（slice），內含 Endpoint、Handler、
Command、Response。

| 層級 | 命名規則 | 範例 |
|------|----------|------|
| **Endpoint** (static class) | `[Action][Aggregate]Endpoint` | `CreateApiKeyEndpoint` |
| **Handler** + interface | `[Action][Aggregate]Handler` + `I[Action][Aggregate]Handler` | `CreateApiKeyHandler`, `ICreateApiKeyHandler` |
| **Command / Query** (record) | `[Action][Aggregate]Command` / `Query` | `CreateApiKeyCommand` |
| **Response** (record) | `[Action][Aggregate]Response` | `CreateApiKeyResponse` |
| **Service** (跨 BC 邊界，經 SharedKernel 暴露) | `[Name]Service` + `I[Name]Service` | `AccessPolicyService`, `IAccessPolicyService` |
| **Repository** (Infrastructure) | `[Aggregate]Repository` + `I[Aggregate]Repository` (interface 在 Domain) | `ApiKeyRepository`, `IApiKeyRepository` |
| **ApiClient** (外部 HTTP 整合) | `[Name]ApiClient` + `I[Name]ApiClient` | `BillingApiClient`, `IBillingApiClient` |
| **Module** (DI + endpoint 註冊聚合) | `[BoundedContext]Module` (static class) | `KeyLifecycleModule` |

> ⚠️ **注意**：外部 API 呼叫使用 `*ApiClient`，不是 `*Client`。

### 專案結構

```
backend/src/
├── Host/                               # Composition root: Program.cs, DI wiring
├── SharedKernel/                       # Cross-BC contracts & primitives
│   ├── Contracts/                      # I*Service interfaces consumed by other BCs
│   ├── Domain/                         # Result, Failure, FailureProvider, AggregateRoot, Entity
│   └── Application/
├── Infrastructure/                     # Cross-cutting persistence + DI module
│   ├── InfrastructureModule.cs         # AddDbContextPool + Repository registrations
│   └── Persistence/
│       ├── AppDbContext.cs
│       ├── Configurations/             # IEntityTypeConfiguration<TEntity>
│       ├── Migrations/
│       └── Repositories/               # Repository implementations
├── {BoundedContext}/                   # e.g. KeyLifecycle, AccessPolicy, TenantManagement, Audit, Monitoring
│   ├── Application/                    # Cross-feature application services within the BC
│   ├── Domain/                         # Aggregates, value objects, domain events, repository interfaces
│   ├── Infrastructure/                 # BC-private infra (rare; most persistence lives in /Infrastructure)
│   ├── {Feature}/                      # Vertical slice — one folder per use case
│   │   ├── {Feature}Endpoint.cs        # Minimal API: app.MapPost / MapGet ...
│   │   ├── {Feature}Handler.cs         # Use case implementation
│   │   ├── I{Feature}Handler.cs        # Handler interface (DI seam, mockable in tests)
│   │   ├── {Feature}Command.cs         # Input record (use Query for read-side)
│   │   └── {Feature}Response.cs        # Output record
│   └── {BoundedContext}Module.cs       # Add{BC}Module + Map{BC}Endpoints extensions
```

> Cross-BC dependencies are forbidden in source — they go through interfaces in
> `SharedKernel/Contracts/`. Architecture tests enforce this via NetArchTest.

---

## B. Microsoft 標準命名

| Element | Convention | Example |
|---------|------------|---------|
| Namespace | PascalCase, BC-rooted | `ApiKeyManagement.KeyLifecycle.CreateApiKey` |
| Class | PascalCase, noun | `ApiKeyRepository`, `CreateApiKeyHandler` |
| Interface | IPascalCase | `IApiKeyRepository`, `IAccessPolicyService` |
| Method | PascalCase, verb | `HandleAsync`, `GetByIdAsync` |
| Property | PascalCase | `TenantId`, `IsActive` |
| Field (private) | _camelCase | `_repository`, `_clock` |
| Field (const) | PascalCase | `MaxRetryCount`, `DefaultTimeout` |
| Parameter | camelCase | `tenantId`, `consumerId` |
| Local variable | camelCase | `activeCount`, `policyId` |
| Enum | PascalCase (singular) | `ApiKeyStatus`, `ScopeKind` |

### Async 方法命名

```csharp
// ✅ Async 方法以 Async 結尾，CancellationToken 命名為 cancel 並給預設值
public async Task<Result<CreateApiKeyResponse, Failure>> HandleAsync(
    CreateApiKeyCommand command,
    CancellationToken cancel = default);

public async Task<int> CountActiveAsync(
    string consumerId, string environment, string tenantId,
    CancellationToken cancel = default);
```

### Boolean 命名

```csharp
// ✅ 使用 is/has/can/should 前綴
public bool IsActive { get; init; }
public bool HasExpired { get; }

// Methods returning bool
public bool IsValid();
public bool HasAccess(ApiKey key);
```

---

## C. 本專案 C# 風格

### File-Scoped Namespaces ✅

```csharp
// ✅ 本專案使用 file-scoped namespace
namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public class CreateApiKeyHandler : ICreateApiKeyHandler
{
}
```

### Primary Constructors ✅

```csharp
// ✅ 本專案 Handler / Service / Repository 一律使用 Primary Constructor (.NET 8+)
// ✅ Service 與 Handler 不注入 ILogger（CLAUDE.md NEVER 規則）—
//    觀測性由 Endpoint / Middleware / Pipeline Behavior 等邊界統一處理。
public class CreateApiKeyHandler(
    IConsumerValidator consumerValidator,
    IApiKeyRepository keyRepository,
    IScopeRegistry scopeRegistry,
    IAccessPolicyService accessPolicyService) : ICreateApiKeyHandler
{
    public async Task<Result<CreateApiKeyResponse, Failure>> HandleAsync(
        CreateApiKeyCommand command, CancellationToken cancel = default)
    {
        // ... Result-based flow; see exceptions.rule.md for Result pattern.
    }
}
```

### Collection 初始化

```csharp
// ✅ 本專案（.NET 10 / C# 13）使用 Collection Expressions
string[] scopes = ["orders:read", "orders:write"];
IReadOnlyList<string> empty = [];

// ✅ 方法參數、屬性初始化皆可使用
ApiKey.Create(scopes: ["seed:read"], ...);
```

---

## D. 註解規範

### XML 文件

```csharp
/// <summary>
/// Issues a new API key for the given tenant + consumer.
/// </summary>
/// <param name="command">Creation parameters validated upstream.</param>
/// <param name="cancel">Cancellation token propagated to all I/O.</param>
/// <returns>The created key on success, otherwise a Failure.</returns>
public async Task<Result<CreateApiKeyResponse, Failure>> HandleAsync(
    CreateApiKeyCommand command,
    CancellationToken cancel = default);
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
// 取得 API Key
var key = await repository.GetByIdAsync(keyId, cancel);

// ✅ GOOD: 解釋為什麼
// Active count is bounded per (consumer, environment, tenant) — guard before insert
// to avoid race-condition over-issue.
var activeCount = await repository.CountActiveAsync(consumerId, environment, tenantId, cancel);
```

---

## E. API 路由命名

本專案使用 **Minimal API**，每個 slice 一個 `static class *Endpoint` 並暴露 `Map(IEndpointRouteBuilder)`。

```csharp
// ✅ 本專案 Endpoint 範例：CreateApiKeyEndpoint.cs
public static class CreateApiKeyEndpoint
{
    public record Request(
        string Name,
        string Environment,
        IReadOnlyList<string> Scopes,
        DateTimeOffset ExpiresAt);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys",
            async (
                string tenantId,
                string consumerId,
                Request request,
                ICreateApiKeyHandler handler,
                CancellationToken cancel) =>
            {
                var command = new CreateApiKeyCommand(
                    TenantId: tenantId,
                    ConsumerId: consumerId,
                    Name: request.Name,
                    Environment: request.Environment,
                    Scopes: request.Scopes,
                    ExpiresAt: request.ExpiresAt);

                var result = await handler.HandleAsync(command, cancel);
                if (result.IsFailure)
                {
                    return result.Error.Code switch
                    {
                        // 將 Failure code 對應到 Results.NotFound / Conflict / UnprocessableEntity / ...
                        _ => Results.Problem(result.Error.Code)
                    };
                }

                return Results.Created(
                    $"/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys/{result.Value.KeyId}",
                    result.Value);
            });
    }
}
```

Endpoint 們由 BC Module 集中註冊：

```csharp
// ✅ KeyLifecycleModule.cs
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

URL 規範：`/api/v{n}/<resource hierarchy>` — lowercase、複數名詞、RESTful 動詞透過 HTTP method 表達。

---

## F. 環境變數命名

```csharp
// ✅ 環境變數使用 UPPER_SNAKE_CASE
public record SYS_REDIS_URL : EnvironmentVariable;
public record PG_EVENT_CONNECTION_STRING : EnvironmentVariable;
public record ASPNETCORE_ENVIRONMENT : EnvironmentVariable;

// 使用方式
public class CacheService(SYS_REDIS_URL redisUrl)
{
    // ADR-005 §3 settings snapshot exception: cache the env var value once at construction.
    private readonly string _connectionString = redisUrl.Value;
}
```

---

## G. 檔案組織

### 一個類別一個檔案

```
✅ 正確
CreateApiKeyHandler.cs     → public class CreateApiKeyHandler
ICreateApiKeyHandler.cs    → public interface ICreateApiKeyHandler
ApiKeyStatus.cs            → public enum ApiKeyStatus

⚠️ 例外：與 Endpoint 緊密耦合的小 record（例如 inbound Request）可以與 Endpoint
   放同一檔，提升 slice 內聚性。
```

### Command / Response 命名

```csharp
// Command: [Action][Aggregate]Command
public record CreateApiKeyCommand(
    string TenantId,
    string ConsumerId,
    string Name,
    string Environment,
    IReadOnlyList<string> Scopes,
    DateTimeOffset ExpiresAt);

// Response: [Action][Aggregate]Response
public record CreateApiKeyResponse(
    Guid KeyId,
    string ConsumerId,
    string TenantId,
    string Name,
    string KeyPrefix,
    string RawKey,
    string Environment,
    IReadOnlyList<string> Scopes,
    string LifecycleStatus,
    Guid PolicyId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
```
