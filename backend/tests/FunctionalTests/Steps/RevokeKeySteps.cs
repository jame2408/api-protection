using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.RevokeKey;
using ApiKeyManagement.KeyLifecycle.RevokeLeakedKeys;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class RevokeKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    // Secret Scanner leak-detection scenario (feature line 44-51): the prefix reported by the
    // scanner doesn't map to any keyAlias, so it's held here between the Given and When steps.
    private string? _leakedPrefix;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    private IApiKeyHasher Hasher =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<IApiKeyHasher>();

    // -------------------------------------------------------------------------
    // Seed helpers
    // -------------------------------------------------------------------------

    // Revoke handler validates neither tenant nor consumer existence (RevokeKeyHandler.cs),
    // so no Tenant/Consumer row is seeded here — CurrentTenantId is only needed for the URL.
    private ApiKey CreateSeedKey(string keyAlias)
    {
        var (key, _) = ApiKey.Create(
            consumerId: "any-consumer",
            tenantId: _ctx.CurrentTenantId,
            name: keyAlias,
            environment: "Production",
            scopes: ["seed:read"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            policyId: Guid.NewGuid(),
            hasher: Hasher);

        Db.ApiKeys.Add(key);
        _ctx.SeededKeys[keyAlias] = key.Id;

        return key;
    }

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given(@"金鑰 ""(.*)"" 狀態為 Active")]
    // 05_RotateKey.feature line 7 — same seed (expiresAt: UtcNow.AddDays(30), see CreateSeedKey)
    // already satisfies "尚未到期"; overlaid rather than duplicated (RotateKeySteps.cs).
    [Given(@"金鑰 ""(.*)"" 狀態為 Active，尚未到期")]
    public async Task GivenKeyIsActive(string keyAlias)
    {
        _ctx.CurrentTenantId = "tenant-A";

        CreateSeedKey(keyAlias);

        await Db.SaveChangesAsync();
    }

    [Given(@"金鑰 ""(.*)"" 狀態為 Rotating，successorKeyId 為 ""(.*)""")]
    public async Task GivenKeyIsRotatingWithSuccessor(string predecessorAlias, string successorAlias)
    {
        _ctx.CurrentTenantId = "tenant-A";

        var predecessor = CreateSeedKey(predecessorAlias);
        var successor = CreateSeedKey(successorAlias);

        // ApiKey properties are all `private set`; the rotation itself (Wave 5 RotateKey) is
        // out of scope here, so seeding sets the Added entities' CurrentValue directly —
        // EF applies it at SaveChanges time, bypassing the private setters without adding a
        // speculative production method just for test seeding.
        Db.Entry(predecessor).Property(k => k.Status).CurrentValue = ApiKeyStatus.Rotating;
        Db.Entry(predecessor).Property(k => k.SuccessorKeyId).CurrentValue = successor.Id;
        Db.Entry(successor).Property(k => k.PredecessorKeyId).CurrentValue = predecessor.Id;

        await Db.SaveChangesAsync();
    }

    [Given(@"金鑰 ""(.*)"" 狀態為 Locked")]
    public async Task GivenKeyIsLocked(string keyAlias)
    {
        _ctx.CurrentTenantId = "tenant-A";

        var key = CreateSeedKey(keyAlias);

        // ApiKey.Status is `private set`; bypass via CurrentValue as in GivenKeyIsRotatingWithSuccessor above.
        Db.Entry(key).Property(k => k.Status).CurrentValue = ApiKeyStatus.Locked;

        await Db.SaveChangesAsync();
    }

    [Given(@"金鑰 ""(.*)"" 狀態為 Suspended")]
    public async Task GivenKeyIsSuspended(string keyAlias)
    {
        _ctx.CurrentTenantId = "tenant-A";

        var key = CreateSeedKey(keyAlias);

        // ApiKey.Status is `private set`; bypass via CurrentValue as in GivenKeyIsRotatingWithSuccessor above.
        Db.Entry(key).Property(k => k.Status).CurrentValue = ApiKeyStatus.Suspended;

        await Db.SaveChangesAsync();
    }

    [Given(@"金鑰 ""(.*)"" 狀態為 Expired")]
    public async Task GivenKeyIsExpired(string keyAlias)
    {
        _ctx.CurrentTenantId = "tenant-A";

        var key = CreateSeedKey(keyAlias);

        // ApiKey.Status is `private set`; bypass via CurrentValue as in GivenKeyIsRotatingWithSuccessor above.
        Db.Entry(key).Property(k => k.Status).CurrentValue = ApiKeyStatus.Expired;

        await Db.SaveChangesAsync();
    }

    [Given(@"金鑰 ""(.*)""（prefix ""(.*)""）狀態為 Active")]
    public async Task GivenKeyIsActiveWithPrefix(string keyAlias, string prefix)
    {
        _ctx.CurrentTenantId = "tenant-A";

        var key = CreateSeedKey(keyAlias);

        // KeyPrefix is `private set` — bypass via CurrentValue as in the other Given steps
        // above; Create() derives its own prefix from tenantId/environment, which doesn't
        // match the scanner-reported literal this scenario needs.
        Db.Entry(key).Property(k => k.KeyPrefix).CurrentValue = prefix;

        await Db.SaveChangesAsync();
    }

    [Given(@"Secret Scanner 在公開儲存庫偵測到 prefix ""(.*)""")]
    public Task GivenSecretScannerDetectsPrefix(string prefix)
    {
        _leakedPrefix = prefix;
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"Secret Scanner 對所有符合該 prefix 的非終態金鑰發出撤銷命令")]
    public async Task WhenSecretScannerRevokesLeakedKeys()
    {
        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            RevokeLeakedKeysEndpoint.Route,
            new RevokeLeakedKeysEndpoint.Request(_leakedPrefix!));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"操作者撤銷 ""(.*)""，原因為「(.*)」")]
    [When(@"Security Admin 撤銷 ""(.*)""，原因為「(.*)」")]
    public async Task WhenOperatorRevokesKey(string keyAlias, string reason)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/revoke",
            new RevokeKeyEndpoint.Request(reason));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"操作者撤銷 ""(.*)""，未提供原因")]
    public async Task WhenOperatorRevokesKeyWithoutReason(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        // Faithful to "未提供原因": POST an empty JSON object rather than an explicit empty
        // string. RevokeKeyEndpoint.Request has no `required` modifier on Reason, so STJ binds
        // the missing property to null, and RevokeKeyHandler guard 2 (IsNullOrWhiteSpace) treats
        // null the same as empty/whitespace.
        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/revoke",
            new { });

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    [Then(@"""(.*)"" 狀態變為 Revoked")]
    public void ThenKeyStatusBecomesRevoked(string keyAlias)
    {
        _ctx.Response!.StatusCode.Should().Be(HttpStatusCode.OK);

        // ADR-006: assert raw JSON literal to lock the wire-format string,
        // not just the round-tripped enum value.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var root = doc.RootElement;

        // Dual wire-shape: single-key revoke (api-spec.md §3.2.8) returns `lifecycleStatus` at
        // root; batch leaked-key revoke (api-spec.md §3.2.9) returns a `revokedKeys` array —
        // locate the element for this alias's keyId instead.
        if (root.TryGetProperty("lifecycleStatus", out var singleStatus))
        {
            singleStatus.GetString().Should().Be("Revoked");
            root.GetProperty("revokedBy").GetString().Should().Be("security-admin-1");
        }
        else
        {
            var keyId = _ctx.SeededKeys[keyAlias];
            var matching = root.GetProperty("revokedKeys").EnumerateArray()
                .Single(e => e.GetProperty("keyId").GetGuid() == keyId);
            matching.GetProperty("lifecycleStatus").GetString().Should().Be("Revoked");
        }

        var reloadedKeyId = _ctx.SeededKeys[keyAlias];

        // AsNoTracking: the Given step above added and saved this same entity on this same
        // scenario-scoped DbContext, so a tracked re-query would return the stale in-memory
        // instance (EF Core identity resolution) instead of reflecting the update made by the
        // HTTP request's own (separate) DbContext instance.
        var reloaded = Db.ApiKeys.AsNoTracking().Single(k => k.Id == reloadedKeyId);
        reloaded.Status.Should().Be(ApiKeyStatus.Revoked, "the transition must be persisted, not just returned on the wire");
    }

    [Then(@"系統產生 KeyRevoked 事件，reason 為 ""(.*)""")]
    public void ThenKeyRevokedEventHasLeakReason(string reason)
    {
        // This scenario (feature line 44) seeds exactly one key and this step's Gherkin text
        // carries no alias, so the scenario's sole seeded keyId is looked up directly.
        var keyId = _ctx.SeededKeys.Values.Single();

        using var payload = Db.RequireOutboxEvent("KeyRevoked", keyId);
        payload.RootElement.GetProperty("reason").GetString().Should().Be(reason);

        var revokedBy = payload.RootElement.GetProperty("revokedBy");
        revokedBy.GetProperty("type").GetString().Should().Be("System");
        revokedBy.GetProperty("id").GetString().Should().Be("secret-scanner");
        revokedBy.GetProperty("name").GetString().Should().Be("Secret Scanner");
    }

    [Then(@"系統通知 Security Admin 和 Consumer")]
    public void ThenSystemNotifiesSecurityAdminAndConsumer()
    {
        var keyId = _ctx.SeededKeys.Values.Single();

        using var payload = Db.RequireOutboxEvent("KeyLeakNotificationRequested", keyId);
        var audiences = payload.RootElement.GetProperty("audiences")
            .EnumerateArray().Select(a => a.GetString()).ToList();

        audiences.Should().Contain("SecurityAdmin");
        audiences.Should().Contain("Consumer");
    }

    [Then(@"清除 ""(.*)"" 與 ""(.*)"" 之間的 successorKeyId / predecessorKeyId 關聯")]
    public void ThenRotationLinkIsCleared(string predecessorAlias, string successorAlias)
    {
        // AsNoTracking: same identity-resolution reasoning as ThenKeyStatusBecomesRevoked above —
        // the Given step added and saved these entities on this scenario-scoped DbContext, so a
        // tracked re-query would return the stale in-memory instance instead of the update made
        // by the HTTP request's own (separate) DbContext instance.
        var predecessorId = _ctx.SeededKeys[predecessorAlias];
        var successorId = _ctx.SeededKeys[successorAlias];

        var predecessor = Db.ApiKeys.AsNoTracking().Single(k => k.Id == predecessorId);
        var successor = Db.ApiKeys.AsNoTracking().Single(k => k.Id == successorId);

        predecessor.SuccessorKeyId.Should().BeNull(
            "design-doc.md T6 requires revoking a Rotating key to clear its own successorKeyId link");
        successor.PredecessorKeyId.Should().BeNull(
            "design-doc.md T6 requires revoking a Rotating key to clear the successor's predecessorKeyId link");
    }

    [Then(@"系統產生 KeyRevoked 事件，previousStatus 為 (.*)")]
    public void ThenKeyRevokedEventIsPublished(string previousStatus)
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var keyId = doc.RootElement.GetProperty("keyId").GetGuid();

        using var payload = Db.RequireOutboxEvent("KeyRevoked", keyId);
        payload.RootElement.GetProperty("previousStatus").GetString().Should().Be(previousStatus);
        payload.RootElement.GetProperty("reason").GetString().Should().NotBeNullOrEmpty();

        var revokedBy = payload.RootElement.GetProperty("revokedBy");
        revokedBy.GetProperty("type").GetString().Should().Be("User");
        revokedBy.GetProperty("id").GetString().Should().Be("security-admin-1");
        revokedBy.GetProperty("name").GetString().Should().Be("security-admin-1");
    }

    [Then(@"觸發主動快取失效")]
    public void ThenActiveCacheInvalidationIsTriggered()
    {
        // context-integration-spec.md §4.7 I7 projection table marks KeyRevoked as requiring
        // active cache invalidation ("是"): the Gateway pub/sub broadcast is driven by the
        // KeyRevoked domain event itself, so the event's presence in the outbox *is* the
        // trigger being asserted here — not a response-body proxy for it.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var root = doc.RootElement;

        // Dual wire-shape (see ThenKeyStatusBecomesRevoked above): batch leaked-key revoke has
        // no root `keyId`, so fall back to the scenario's sole seeded key.
        var keyId = root.TryGetProperty("keyId", out var keyIdProp)
            ? keyIdProp.GetGuid()
            : _ctx.SeededKeys.Values.Single();

        Db.OutboxMessages.Any(m => m.EventType == "KeyRevoked" && m.AggregateId == keyId.ToString())
            .Should().BeTrue("KeyRevoked must reach the outbox to trigger active cache invalidation");
    }

    [Then(@"撤銷失敗，錯誤原因為「(.*)」")]
    public void ThenRevokeFailsWithReason(string reason)
    {
        var map = new Dictionary<string, (HttpStatusCode Status, string ErrorCode)>
        {
            // API wire contract — keep literals here to lock external HTTP error codes.
            // Production code uses *FailureCodes.* constants; this map intentionally
            // re-states the strings so a constant value drift would surface as a test failure.
            ["金鑰已在終態，無法撤銷"] = (HttpStatusCode.Conflict, "KEY_IN_TERMINAL_STATE"),
            ["必須提供撤銷原因"] = (HttpStatusCode.BadRequest, "VALIDATION_ERROR:reason_empty"),
        };

        var entry = map.First(kv => reason.StartsWith(kv.Key, StringComparison.Ordinal));
        var (expectedStatus, expectedErrorCode) = entry.Value;

        // RFC 9457 Problem Details wire contract (api-spec.md §2.2). Locks every failure scenario
        // that uses this step — including the @ignore'd ones, as they come online.
        ProblemAssertions.RequireProblem(_ctx.Response!, _ctx.ResponseBody!, expectedStatus, expectedErrorCode);
    }
}
