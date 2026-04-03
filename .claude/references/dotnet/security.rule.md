# .NET 安全規則

.NET 後端開發的安全模式與反模式。

---

## A. Critical Security Issues

### 🔒 SQL Injection

| Risk Level | Pattern | Example |
|------------|---------|---------|
| 🔴 Critical | SQL 字串串接 | `$"SELECT * FROM Users WHERE Id = {id}"` |
| 🔴 Critical | Raw SQL 內插字串 | `ExecuteSqlRaw($"DELETE FROM {table}")` |

```csharp
// ❌ SQL Injection 風險
var query = $"SELECT * FROM Users WHERE Email = '{email}'";
await context.Database.ExecuteSqlRawAsync(query);

// ❌ 同樣有風險
var sql = "SELECT * FROM Users WHERE Email = '" + email + "'";

// ✅ Parameterized Query (EF Core)
await context.Users.Where(u => u.Email == email).ToListAsync(cancel);

// ✅ Parameterized Raw SQL
await context.Database.ExecuteSqlRawAsync(
    "SELECT * FROM Users WHERE Email = {0}", email);

// ✅ 使用 FromSqlInterpolated（安全的內插）
await context.Users.FromSqlInterpolated(
    $"SELECT * FROM Users WHERE Email = {email}").ToListAsync(cancel);
```

---

### 🔑 Hardcoded Secrets

| Risk Level | Pattern | Example |
|------------|---------|---------|
| 🔴 Critical | 程式碼中的密碼 | `password = "abc123"` |
| 🔴 Critical | 程式碼中的 API Key | `apiKey = "sk-xxx"` |
| 🔴 Critical | 程式碼中的連線字串 | `Server=prod;Password=secret` |

```csharp
// ❌ Hardcoded secrets
var connectionString = "Server=myServer;Password=MyP@ssw0rd;";
var apiKey = "sk-1234567890abcdef";

// ✅ 使用環境變數（本專案模式）
public class MyService(SYS_REDIS_URL redisUrl, API_KEY apiKey)
{
    private readonly string _connectionString = redisUrl.Value;
    private readonly string _apiKey = apiKey.Value;
}

// ✅ 或使用 IConfiguration
var connectionString = configuration.GetConnectionString("Default");
```

---

### 🚪 Missing Authorization

| Risk Level | Pattern | Example |
|------------|---------|---------|
| 🔴 Critical | 敏感資料的公開端點 | 沒有 `[Authorize]` 的管理端點 |
| 🟡 High | 缺少角色檢查 | 管理操作沒有 `[Authorize(Roles="Admin")]` |

```csharp
// ❌ 缺少授權
[HttpGet("users/{id}/profile")]
public async Task<IActionResult> GetUserProfile(int id)
{
    return Ok(await _userService.GetProfile(id));
}

// ✅ 本專案使用 [Authenticate] 或 [Authorize]
[Authenticate]
[HttpGet("users/{id}/profile")]
public async Task<IActionResult> GetUserProfile(int id, CancellationToken cancel = default)
{
    // 檢查使用者是否有權存取此 profile（防止 IDOR）
    var talentNo = User.GetTalentNo();
    if (talentNo != id)
    {
        return this.Failure(FailureProvider.CreateFailure(ErrorCode.Forbidden));
    }
    
    var result = await _userService.GetProfileAsync(id, cancel);
    if (result.IsFailure)
    {
        return this.Failure(result.Error);
    }
    
    return Ok(result.Value);
}
```

---

### 🌐 SSRF (Server-Side Request Forgery)

| Risk Level | Pattern | Example |
|------------|---------|---------|
| 🔴 Critical | 使用者可控制的 URL 進行 HTTP 呼叫 | `HttpClient.GetAsync(userProvidedUrl)` |

```csharp
// ❌ SSRF 風險 - 使用者可存取內部服務
[HttpGet("fetch")]
public async Task<IActionResult> FetchUrl([FromQuery] string url)
{
    var content = await _httpClient.GetStringAsync(url); // 危險!
    return Ok(content);
}

// ✅ 驗證並白名單 URL
[HttpGet("fetch")]
public async Task<IActionResult> FetchUrl([FromQuery] string url, CancellationToken cancel = default)
{
    if (!IsAllowedUrl(url))
    {
        return this.Failure(FailureProvider.CreateFailure(ErrorCode.Forbidden, "URL not allowed"));
    }
    
    var content = await _httpClient.GetStringAsync(url, cancel);
    return Ok(content);
}

private static bool IsAllowedUrl(string url)
{
    var uri = new Uri(url);
    var allowedHosts = new[] { "api.1111.com.tw", "cdn.1111.com.tw" };
    return allowedHosts.Any(h => uri.Host.EndsWith(h, StringComparison.OrdinalIgnoreCase));
}
```

---

### 📁 Path Traversal

| Risk Level | Pattern | Example |
|------------|---------|---------|
| 🔴 Critical | 使用者輸入用於檔案路徑 | `File.ReadAllText(userPath)` |

```csharp
// ❌ Path Traversal 風險
[HttpGet("files")]
public IActionResult GetFile([FromQuery] string filename)
{
    var path = Path.Combine("uploads", filename);
    return File(System.IO.File.ReadAllBytes(path), "application/octet-stream");
    // 使用者可傳入 "../../../etc/passwd" 或 "..\..\web.config"
}

// ✅ 驗證並清理路徑
[HttpGet("files")]
public IActionResult GetFile([FromQuery] string filename)
{
    // 移除路徑分隔符
    var safeName = Path.GetFileName(filename);
    
    var basePath = Path.GetFullPath("uploads");
    var fullPath = Path.GetFullPath(Path.Combine(basePath, safeName));
    
    // 確保路徑在允許的目錄內
    if (!fullPath.StartsWith(basePath))
    {
        return this.Failure(FailureProvider.CreateFailure(ErrorCode.Forbidden, "Invalid filename"));
    }
    
    if (!System.IO.File.Exists(fullPath))
    {
        return this.Failure(FailureProvider.CreateFailure(ErrorCode.NotFound));
    }
    
    return File(System.IO.File.ReadAllBytes(fullPath), "application/octet-stream");
}
```

---

## B. Input Validation

### 本專案使用 FluentValidation

```csharp
// ✅ Request 搭配 FluentValidation
public class CreateOrderRequest
{
    public string CustomerName { get; set; }
    public string Email { get; set; }
    public decimal Amount { get; set; }
    public List<OrderItemRequest> Items { get; set; }
}

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .MaximumLength(100);
        
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
        
        RuleFor(x => x.Amount)
            .GreaterThan(0);
        
        RuleFor(x => x.Items)
            .NotEmpty()
            .Must(items => items.Sum(i => i.Quantity) <= 100)
            .WithMessage("Total quantity cannot exceed 100");
    }
}
```

---

## C. Sensitive Data Protection

### Logging

```csharp
// ❌ 記錄敏感資料
_logger.LogInformation("User login: {Email}, Password: {Password}", email, password);

// ✅ 遮蔽敏感資料
_logger.LogInformation("User login: {Email}", email);

// ✅ 使用結構化日誌記錄安全的資料
_logger.LogInformation("User {UserId} logged in from {IpAddress}", userId, ipAddress);
```

### Response Data

```csharp
// ❌ 暴露內部細節
public class UserResponse
{
    public string PasswordHash { get; set; }  // 絕對不要暴露!
    public string SecurityStamp { get; set; } // 絕對不要暴露!
}

// ✅ 使用 DTO 只包含必要欄位
public class UserResponse
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}
```

---

## D. Code Review Detection Patterns

| Issue | Detection Pattern | Severity |
|-------|-------------------|----------|
| SQL Injection | `$"SELECT...{var}"`, `+ variable +` in SQL | 🔴 Critical |
| Hardcoded Secrets | `password =`, `apiKey =`, `connectionString =` literals | 🔴 Critical |
| Missing Auth | 敏感端點沒有 `[Authorize]` 或 `[Authenticate]` | 🔴 Critical |
| SSRF | `HttpClient.Get*(userInput)` | 🔴 Critical |
| Path Traversal | `File.*` 使用使用者輸入 | 🔴 Critical |
| XSS | `Html.Raw(userInput)` | 🔴 Critical |
| Sensitive Logging | `Log*(...password...)` | 🟡 High |
| IDOR | 授權後沒有檢查資源所有權 | 🟡 High |
