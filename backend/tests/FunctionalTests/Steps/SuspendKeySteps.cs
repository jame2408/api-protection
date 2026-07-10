using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.SuspendKey;
using ApiKeyManagement.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class SuspendKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given(@"操作者為 Security Admin（人為操作者）")]
    public void GivenOperatorIsSecurityAdmin()
    {
        // Same default TestHooks already wires up (TestHooks.cs:101-103) — declared explicitly
        // here so later scenarios in this feature that swap the actor (System / Consumer) have
        // a precedent for re-issuing the token and re-setting this header.
        _ctx.AuthToken = TestTokenFactory.CreateSecurityAdminToken();
        _ctx.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ctx.AuthToken);
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"Security Admin 暫停 ""(.*)""，原因為「(.*)」")]
    public async Task WhenSecurityAdminSuspendsKey(string keyAlias, string reason)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/suspend",
            new SuspendKeyEndpoint.Request(reason));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    [Then(@"""(.*)"" 狀態變為 Suspended")]
    public void ThenKeyStatusBecomesSuspended(string keyAlias)
    {
        _ctx.Response!.StatusCode.Should().Be(HttpStatusCode.OK);

        // ADR-006: assert raw JSON literal to lock the wire-format string,
        // not just the round-tripped enum value.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("lifecycleStatus").GetString().Should().Be("Suspended");

        var reloadedKeyId = _ctx.SeededKeys[keyAlias];

        // AsNoTracking: the Given step above added and saved this same entity on this same
        // scenario-scoped DbContext, so a tracked re-query would return the stale in-memory
        // instance (EF Core identity resolution) instead of reflecting the update made by the
        // HTTP request's own (separate) DbContext instance.
        var reloaded = Db.ApiKeys.AsNoTracking().Single(k => k.Id == reloadedKeyId);
        reloaded.Status.Should().Be(ApiKeyStatus.Suspended, "the transition must be persisted, not just returned on the wire");
    }

    [Then(@"系統產生 KeySuspended 事件，包含 keyId、suspendedBy、reason")]
    public void ThenKeySuspendedEventIsPublished()
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var keyId = doc.RootElement.GetProperty("keyId").GetGuid();

        using var payload = Db.RequireOutboxEvent("KeySuspended", keyId);
        var root = payload.RootElement;

        root.GetProperty("keyId").GetGuid().Should().Be(keyId);

        // suspendedBy is a nested Actor object on the wire (integration spec §6.1 / §3 Actor
        // schema) — distinct from the response's flat `suspendedBy` string (api-spec.md §3.2.5).
        var suspendedBy = root.GetProperty("suspendedBy");
        suspendedBy.GetProperty("type").GetString().Should().Be("User");
        suspendedBy.GetProperty("id").GetString().Should().Be("security-admin-1");
        suspendedBy.GetProperty("name").GetString().Should().Be("security-admin-1");

        root.GetProperty("reason").GetString().Should().Be("維護排程");
    }
}
