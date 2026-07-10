using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.RevokeKey;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class RevokeKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

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

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

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
        doc.RootElement.GetProperty("lifecycleStatus").GetString().Should().Be("Revoked");

        var keyId = _ctx.SeededKeys[keyAlias];

        // AsNoTracking: the Given step above added and saved this same entity on this same
        // scenario-scoped DbContext, so a tracked re-query would return the stale in-memory
        // instance (EF Core identity resolution) instead of reflecting the update made by the
        // HTTP request's own (separate) DbContext instance.
        var reloaded = Db.ApiKeys.AsNoTracking().Single(k => k.Id == keyId);
        reloaded.Status.Should().Be(ApiKeyStatus.Revoked, "the transition must be persisted, not just returned on the wire");
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

        // Seeding via ApiKey.Create() also emits a KeyCreated event into the same outbox
        // (Given step above), so this scenario's outbox is never expected to hold exactly
        // one row — filter by EventType to isolate the KeyRevoked row under test
        // (ADR-020 §4 assertion contract).
        var outboxRow = Db.OutboxMessages.SingleOrDefault(m =>
            m.EventType == "KeyRevoked" && m.AggregateId == keyId.ToString());

        outboxRow.Should().NotBeNull("a KeyRevoked domain event must be harvested into the outbox (ADR-020)");

        using var payload = JsonDocument.Parse(outboxRow!.Payload);
        payload.RootElement.GetProperty("previousStatus").GetString().Should().Be(previousStatus);
        payload.RootElement.GetProperty("reason").GetString().Should().NotBeNullOrEmpty();
    }

    [Then(@"觸發主動快取失效")]
    public void ThenActiveCacheInvalidationIsTriggered()
    {
        // context-integration-spec.md §4.7 I7 projection table marks KeyRevoked as requiring
        // active cache invalidation ("是"): the Gateway pub/sub broadcast is driven by the
        // KeyRevoked domain event itself, so the event's presence in the outbox *is* the
        // trigger being asserted here — not a response-body proxy for it.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var keyId = doc.RootElement.GetProperty("keyId").GetGuid();

        Db.OutboxMessages.Any(m => m.EventType == "KeyRevoked" && m.AggregateId == keyId.ToString())
            .Should().BeTrue("KeyRevoked must reach the outbox to trigger active cache invalidation");
    }
}
