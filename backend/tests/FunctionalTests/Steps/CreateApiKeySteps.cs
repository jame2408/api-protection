using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.CreateApiKey;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.TenantManagement.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class CreateApiKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false),
        },
    };

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    private IApiKeyHasher Hasher =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<IApiKeyHasher>();

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given(@"租戶 ""(.*)"" 狀態為 Active")]
    public async Task GivenTenantIsActive(string tenantId)
    {
        _ctx.CurrentTenantId = tenantId;
        Db.Tenants.Add(new Tenant(tenantId, TenantStatus.Active));
        await Db.SaveChangesAsync();
    }

    [Given(@"租戶 ""(.*)"" 不存在")]
    public void GivenTenantDoesNotExist(string tenantId)
    {
        _ctx.CurrentTenantId = tenantId;
        // no-op: tenant is absent from DB by default
    }

    [Given(@"租戶 ""(.*)"" 狀態為 Suspended")]
    public async Task GivenTenantIsSuspended(string tenantId)
    {
        _ctx.CurrentTenantId = tenantId;
        Db.Tenants.Add(new Tenant(tenantId, TenantStatus.Suspended));
        await Db.SaveChangesAsync();
    }

    [Given(@"Consumer ""(.*)"" 屬於 ""(.*)""")]
    public async Task GivenConsumerBelongsToTenant(string consumerId, string tenantId)
    {
        Db.Consumers.Add(new Consumer(consumerId, tenantId));
        await Db.SaveChangesAsync();
    }

    [Given(@"Consumer ""(.*)"" 不屬於 ""(.*)""")]
    public async Task GivenConsumerDoesNotBelongToTenant(string consumerId, string tenantId)
    {
        _ctx.CurrentTenantId = tenantId;
        Db.Tenants.Add(new Tenant(tenantId, TenantStatus.Active));
        await Db.SaveChangesAsync();
        // consumer intentionally absent from DB
    }

    [Given(@"""(.*)"" 在 Production 環境的 Active 金鑰數為 (\d+)，上限為 (\d+)")]
    public async Task GivenActiveKeyCount(string consumerId, int current, int limit)
    {
        // Scenarios reaching this step directly (without a prior tenant/consumer Given)
        // need tenant + consumer seeded so the I1 validator passes before the key-count guard runs.
        if (string.IsNullOrEmpty(_ctx.CurrentTenantId))
        {
            _ctx.CurrentTenantId = "tenant-A";
            Db.Tenants.Add(new Tenant("tenant-A", TenantStatus.Active));
            Db.Consumers.Add(new Consumer(consumerId, "tenant-A"));
            await Db.SaveChangesAsync();
        }

        // Seed `current` Active keys for this consumer in Production
        for (var i = 0; i < current; i++)
        {
            var (key, _) = ApiKey.Create(
                consumerId: consumerId,
                tenantId: _ctx.CurrentTenantId,
                name: $"seed-key-{i}",
                environment: "Production",
                scopes: ["seed:read"],
                expiresAt: DateTimeOffset.UtcNow.AddDays(30),
                policyId: Guid.NewGuid(),
                hasher: Hasher);

            Db.ApiKeys.Add(key);
        }

        await Db.SaveChangesAsync();
    }

    [Given(@"""(.*)"" 在 Production 環境沒有名為 ""(.*)"" 的金鑰")]
    public void GivenKeyNameNotExists(string consumerId, string keyName)
    {
        // no-op: key absent from DB by default
    }

    [Given(@"""(.*)"" 在 Production 環境已有名為 ""(.*)"" 的金鑰")]
    public async Task GivenKeyNameAlreadyExists(string consumerId, string keyName)
    {
        // Scenarios reaching this step directly (without a prior tenant/consumer Given)
        // need tenant + consumer seeded so the I1 validator passes before the key-name-duplicate guard runs.
        if (string.IsNullOrEmpty(_ctx.CurrentTenantId))
        {
            _ctx.CurrentTenantId = "tenant-A";
            Db.Tenants.Add(new Tenant("tenant-A", TenantStatus.Active));
            Db.Consumers.Add(new Consumer(consumerId, "tenant-A"));
            await Db.SaveChangesAsync();
        }

        var (key, _) = ApiKey.Create(
            consumerId: consumerId,
            tenantId: _ctx.CurrentTenantId,
            name: keyName,
            environment: "Production",
            scopes: ["seed:read"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            policyId: Guid.NewGuid(),
            hasher: Hasher);

        Db.ApiKeys.Add(key);
        await Db.SaveChangesAsync();
    }

    [Given(@"Scopes ""(.*)"", ""(.*)"" 已在 Scope Registry 註冊")]
    public async Task GivenScopesRegistered(string scope1, string scope2)
    {
        Db.ScopeRegistryEntries.AddRange(
            new ScopeRegistryEntry(scope1),
            new ScopeRegistryEntry(scope2));
        await Db.SaveChangesAsync();
    }

    [Given(@"Scope ""(.*)"" 未在 Scope Registry 註冊")]
    public void GivenScopeNotRegistered(string scope)
    {
        // no-op: scope absent from DB by default
    }

    [Given(@"指定到期時間為 (\d+) 天後，未超過最大允許有效期")]
    public void GivenExpiresInDays(int days)
    {
        _ctx.ExpiresInDays = days;
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"""(.*)"" 在 Production 環境建立金鑰，名稱 ""(.*)""，scopes \[""(.*)"", ""(.*)""\]，到期 (\d+) 天後")]
    public async Task WhenCreateApiKey(string consumerId, string keyName, string scope1, string scope2,
        int expiresInDays)
    {
        var request = new CreateApiKeyEndpoint.Request(
            Name: keyName,
            Environment: "Production",
            Scopes: [scope1, scope2],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(expiresInDays));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/consumers/{consumerId}/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"Consumer 嘗試在 ""(.*)"" 下建立金鑰")]
    public async Task WhenConsumerTriesToCreateKeyUnderTenant(string tenantId)
    {
        var request = new CreateApiKeyEndpoint.Request(
            Name: "any-key",
            Environment: "Production",
            Scopes: ["any:read"],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/consumers/any-consumer/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"""(.*)"" 嘗試在 ""(.*)"" 下建立金鑰")]
    public async Task WhenSpecificConsumerTriesToCreateKey(string consumerId, string tenantId)
    {
        var request = new CreateApiKeyEndpoint.Request(
            Name: "any-key",
            Environment: "Production",
            Scopes: ["any:read"],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"""(.*)"" 在 Production 環境建立新金鑰")]
    public async Task WhenConsumerCreatesNewKey(string consumerId)
    {
        var request = new CreateApiKeyEndpoint.Request(
            Name: "new-key",
            Environment: "Production",
            Scopes: ["any:read"],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/consumers/{consumerId}/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"""(.*)"" 在 Production 環境建立名為 ""(.*)"" 的金鑰")]
    public async Task WhenConsumerCreatesKeyWithName(string consumerId, string keyName)
    {
        var request = new CreateApiKeyEndpoint.Request(
            Name: keyName,
            Environment: "Production",
            Scopes: ["any:read"],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/consumers/{consumerId}/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // Seed default preconditions (tenant, consumer, default scope "any:read") for scenarios
    // whose only Given describes an absence (e.g. unregistered scope) and thus reach the
    // When step with no tenant/consumer/scope seeded; this lets the request pass every guard
    // ahead of the guard under test.
    private async Task SeedDefaultPreconditionsIfMissingAsync(string consumerId)
    {
        if (!string.IsNullOrEmpty(_ctx.CurrentTenantId))
            return;

        _ctx.CurrentTenantId = "tenant-A";
        Db.Tenants.Add(new Tenant("tenant-A", TenantStatus.Active));
        Db.Consumers.Add(new Consumer(consumerId, "tenant-A"));
        Db.ScopeRegistryEntries.Add(new ScopeRegistryEntry("any:read"));
        await Db.SaveChangesAsync();
    }

    [When(@"Consumer 建立金鑰，scopes 包含 ""(.*)""")]
    public async Task WhenConsumerCreatesKeyWithScope(string scope)
    {
        await SeedDefaultPreconditionsIfMissingAsync("any-consumer");

        var request = new CreateApiKeyEndpoint.Request(
            Name: "any-key",
            Environment: "Production",
            Scopes: [scope],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/consumers/any-consumer/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"Consumer 建立金鑰，scopes 為空")]
    public async Task WhenConsumerCreatesKeyWithEmptyScopes()
    {
        await SeedDefaultPreconditionsIfMissingAsync("any-consumer");

        var request = new CreateApiKeyEndpoint.Request(
            Name: "any-key",
            Environment: "Production",
            Scopes: [],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/consumers/any-consumer/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"Consumer 建立金鑰，到期時間為昨天")]
    public async Task WhenConsumerCreatesKeyExpiredYesterday()
    {
        await SeedDefaultPreconditionsIfMissingAsync("any-consumer");

        var request = new CreateApiKeyEndpoint.Request(
            Name: "any-key",
            Environment: "Production",
            Scopes: ["any:read"],
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/consumers/any-consumer/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"Consumer 建立金鑰，到期時間為 5 年後")]
    public async Task WhenConsumerCreatesKeyExpiresIn5Years()
    {
        await SeedDefaultPreconditionsIfMissingAsync("any-consumer");

        var request = new CreateApiKeyEndpoint.Request(
            Name: "any-key",
            Environment: "Production",
            Scopes: ["any:read"],
            ExpiresAt: DateTimeOffset.UtcNow.AddYears(5));

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/consumers/any-consumer/keys",
            request);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    [Then(@"金鑰狀態為 Active")]
    public void ThenKeyStatusIsActive()
    {
        _ctx.Response!.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        // ADR-006: assert raw JSON literal to lock the wire-format string,
        // not just the round-tripped enum value.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
    }

    [Then(@"系統產生 KeyCreated 事件，包含 keyId、consumerId、tenantId、environment、scopes、keyPrefix、expiresAt、policyId")]
    public void ThenKeyCreatedEventIsPublished()
    {
        var body = JsonSerializer.Deserialize<CreateApiKeyResponse>(_ctx.ResponseBody!, JsonOptions);
        body.Should().NotBeNull();

        body!.KeyId.Should().NotBe(Guid.Empty);
        body.ConsumerId.Should().NotBeNullOrEmpty();
        body.TenantId.Should().NotBeNullOrEmpty();
        body.Environment.Should().NotBeNullOrEmpty();
        body.Scopes.Should().NotBeEmpty();
        body.KeyPrefix.Should().NotBeNullOrEmpty();
        body.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        body.PolicyId.Should().NotBe(Guid.Empty);
    }

    [Then(@"系統回傳金鑰明文（Display Once）")]
    public void ThenRawKeyIsReturned()
    {
        var body = JsonSerializer.Deserialize<CreateApiKeyResponse>(_ctx.ResponseBody!, JsonOptions);
        body.Should().NotBeNull();
        body!.RawKey.Should().NotBeNullOrEmpty();
        body.RawKey.Should().StartWith("apk_");

        // truncatedKey: display-safe suffix "..." + last 4 of rawKey (api-spec.md §2.2).
        body.TruncatedKey.Should().MatchRegex(@"^\.\.\..{4}$");
        body.TruncatedKey.Should().Be("..." + body.RawKey[^4..]);
    }

    [Then(@"同一交易內建立預設 AccessPolicy")]
    public void ThenDefaultAccessPolicyIsCreated()
    {
        var body = JsonSerializer.Deserialize<CreateApiKeyResponse>(_ctx.ResponseBody!, JsonOptions);
        body.Should().NotBeNull();

        var policyId = body!.PolicyId;
        var exists = Db.AccessPolicies.Any(p => p.Id == policyId);
        exists.Should().BeTrue();
    }

    [Then(@"建立失敗，錯誤原因為「(.*)」")]
    public void ThenCreateFailsWithReason(string reason)
    {
        var map = new Dictionary<string, (HttpStatusCode Status, string ErrorCode)>
        {
            // API wire contract — keep literals here to lock external HTTP error codes.
            // Production code uses *FailureCodes.* constants; this map intentionally
            // re-states the strings so a constant value drift would surface as a test failure.
            ["租戶不存在"]            = (HttpStatusCode.NotFound,            "TENANT_NOT_FOUND"),
            ["租戶未啟用"]            = (HttpStatusCode.Forbidden,           "TENANT_SUSPENDED"),
            ["Consumer 不屬於該租戶"] = (HttpStatusCode.NotFound,            "CONSUMER_NOT_FOUND"),
            ["超過金鑰數量上限"]      = (HttpStatusCode.Conflict,            "KEY_LIMIT_EXCEEDED"),
            ["金鑰名稱重複"]          = (HttpStatusCode.Conflict,            "KEY_NAME_DUPLICATE"),
            ["Scope 不存在"]          = (HttpStatusCode.UnprocessableEntity, "SCOPE_NOT_FOUND"),
            ["至少需要一個 Scope"]    = (HttpStatusCode.BadRequest,          "VALIDATION_ERROR:scopes_empty"),
            ["到期時間必須在未來"]    = (HttpStatusCode.BadRequest,          "VALIDATION_ERROR:expires_at_past"),
            ["超過最大允許有效期"]    = (HttpStatusCode.UnprocessableEntity, "EXPIRES_AT_EXCEEDS_MAX"),
        };

        var entry = map.First(kv => reason.StartsWith(kv.Key));
        var (expectedStatus, expectedErrorCode) = entry.Value;

        _ctx.Response!.StatusCode.Should().Be(expectedStatus);
        _ctx.Response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/problem+json");

        // RFC 9457 Problem Details wire contract (api-spec.md §2.2). Locks every failure scenario
        // that uses this step — including the @ignore'd ones, as they come online.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var root = doc.RootElement;
        root.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        root.GetProperty("errorCode").GetString().Should().Be(expectedErrorCode);
        root.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("type").GetString().Should().EndWith(Kebab(expectedErrorCode));
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrEmpty();
    }

    // Mirror of ApiProblem.ToKebab — the wire `type` suffix derives from the error code.
    private static string Kebab(string code)
        => code.ToLowerInvariant().Replace('_', '-').Replace(':', '-');
}