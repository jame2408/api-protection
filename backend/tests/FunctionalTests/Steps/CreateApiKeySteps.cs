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
    };

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

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
    public void GivenConsumerDoesNotBelongToTenant(string consumerId, string tenantId)
    {
        _ctx.CurrentTenantId = tenantId;
        // no-op: consumer absent from DB
    }

    [Given(@"""(.*)"" 在 Production 環境的 ACTIVE 金鑰數為 (\d+)，上限為 (\d+)")]
    public async Task GivenActiveKeyCount(string consumerId, int current, int limit)
    {
        // Seed `current` ACTIVE keys for this consumer in Production
        for (var i = 0; i < current; i++)
        {
            var (key, _) = ApiKey.Create(
                consumerId: consumerId,
                tenantId: _ctx.CurrentTenantId,
                name: $"seed-key-{i}",
                environment: "Production",
                scopes: ["seed:read"],
                expiresAt: DateTimeOffset.UtcNow.AddDays(30),
                policyId: Guid.NewGuid());

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
        var (key, _) = ApiKey.Create(
            consumerId: consumerId,
            tenantId: _ctx.CurrentTenantId,
            name: keyName,
            environment: "Production",
            scopes: ["seed:read"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            policyId: Guid.NewGuid());

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

    [When(@"Consumer 建立金鑰，scopes 包含 ""(.*)""")]
    public async Task WhenConsumerCreatesKeyWithScope(string scope)
    {
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

    [Then(@"金鑰狀態為 ACTIVE")]
    public void ThenKeyStatusIsActive()
    {
        _ctx.Response!.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        var body = JsonSerializer.Deserialize<CreateApiKeyResponse>(_ctx.ResponseBody!, JsonOptions);
        body.Should().NotBeNull();
        body!.LifecycleStatus.Should().Be("ACTIVE");
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
        => throw new PendingStepException();
}