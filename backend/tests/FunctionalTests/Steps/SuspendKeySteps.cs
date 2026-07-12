using System.Net;
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
    [Given(@"操作者為 Security Admin")]
    public void GivenOperatorIsSecurityAdmin()
    {
        // Same default TestHooks already wires up (TestHooks.cs:101-103) — declared explicitly
        // here so later scenarios in this feature that swap the actor (System / Consumer) have
        // a precedent for re-issuing the token and re-setting this header.
        _ctx.AuthenticateAs(TestTokenFactory.CreateSecurityAdminToken());
    }

    [Given(@"操作者為一般 Consumer（無暫停權限）")]
    [Given(@"操作者為一般 Consumer（無恢復權限）")]
    [Given(@"操作者為一般 Consumer")]
    public void GivenOperatorIsConsumerWithoutSuspendPermission()
    {
        _ctx.AuthenticateAs(TestTokenFactory.CreateConsumerToken());
    }

    [Given(@"操作者具備恢復權限")]
    public void GivenOperatorHasResumePermission()
    {
        // ResumeKeyEndpoint currently only requires authentication (no role policy yet — see
        // ResumeKeyEndpoint.Map comment), so any authenticated actor has "resume permission"
        // today. SecurityAdmin here mirrors GivenOperatorIsSecurityAdmin so the KeyResumed
        // event's resumedBy assertion has a stable actor id ("security-admin-1").
        _ctx.AuthenticateAs(TestTokenFactory.CreateSecurityAdminToken());
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

    [When(@"操作者暫停 ""(.*)""，原因為「(.*)」")]
    public async Task WhenOperatorSuspendsKey(string keyAlias, string reason)
    {
        // Token already issued by the Given step above (Consumer here) — do not re-issue it,
        // this step must work for whichever actor a scenario's Given set up.
        var keyId = _ctx.SeededKeys[keyAlias];

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/suspend",
            new SuspendKeyEndpoint.Request(reason));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"Security Admin 暫停 ""(.*)""，未提供原因")]
    public async Task WhenSecurityAdminSuspendsKeyWithoutReason(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        // Faithful to "未提供原因": POST an empty JSON object rather than an explicit empty
        // string. SuspendKeyEndpoint.Request has no `required` modifier on Reason, so STJ binds
        // the missing property to null, and SuspendKeyHandler guard 3 (IsNullOrWhiteSpace) treats
        // null the same as empty/whitespace. Mirrors RevokeKeySteps.WhenOperatorRevokesKeyWithoutReason.
        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/suspend",
            new { });

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"System（非人為操作者）對 ""(.*)"" 發出暫停命令")]
    public async Task WhenSystemSuspendsKey(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        // Legitimate reason + Active seed on purpose: proves the rejection comes from the
        // actor-type guard, not the reason-required or state-transition guards.
        _ctx.AuthenticateAs(TestTokenFactory.CreateSystemToken());

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/suspend",
            new SuspendKeyEndpoint.Request("維護排程"));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"操作者恢復 ""(.*)""")]
    public async Task WhenOperatorResumesKey(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        // api-spec.md §3.2.6: POST /resume has no request body.
        await _ctx.PostNoBodyAndCaptureAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/resume");
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

    [Then(@"""(.*)"" 狀態變為 Active")]
    public void ThenKeyStatusBecomesActive(string keyAlias)
    {
        _ctx.Response!.StatusCode.Should().Be(HttpStatusCode.OK);

        // ADR-006: assert raw JSON literal to lock the wire-format string,
        // not just the round-tripped enum value.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("lifecycleStatus").GetString().Should().Be("Active");

        var reloadedKeyId = _ctx.SeededKeys[keyAlias];

        // AsNoTracking: mirrors ThenKeyStatusBecomesSuspended — the Given step above added and
        // saved this same entity on this same scenario-scoped DbContext, so a tracked re-query
        // would return the stale in-memory instance instead of reflecting the HTTP request's
        // (separate DbContext instance's) update.
        var reloaded = Db.ApiKeys.AsNoTracking().Single(k => k.Id == reloadedKeyId);
        reloaded.Status.Should().Be(ApiKeyStatus.Active, "the transition must be persisted, not just returned on the wire");
    }

    [Then(@"系統產生 KeyResumed 事件，包含 keyId、resumedBy")]
    public void ThenKeyResumedEventIsPublished()
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var keyId = doc.RootElement.GetProperty("keyId").GetGuid();

        using var payload = Db.RequireOutboxEvent("KeyResumed", keyId);
        var root = payload.RootElement;

        root.GetProperty("keyId").GetGuid().Should().Be(keyId);

        // resumedBy is a nested Actor object on the wire (integration spec §6.1 / §3 Actor
        // schema) — distinct from the response's flat `resumedBy` string (api-spec.md §3.2.6).
        var resumedBy = root.GetProperty("resumedBy");
        resumedBy.GetProperty("type").GetString().Should().Be("User");
        resumedBy.GetProperty("id").GetString().Should().Be("security-admin-1");
        resumedBy.GetProperty("name").GetString().Should().Be("security-admin-1");

        // KeyResumed has no Reason field (unlike KeySuspended) — lock the wire shape so a
        // future edit doesn't silently reintroduce one.
        root.TryGetProperty("reason", out _).Should().BeFalse();
    }

    [Then(@"暫停失敗，錯誤原因為「(.*)」")]
    public void ThenSuspendFailsWithReason(string reason)
    {
        var map = new Dictionary<string, (HttpStatusCode Status, string ErrorCode)>
        {
            // API wire contract — keep literals here to lock external HTTP error codes.
            // Production code uses *FailureCodes.* constants; this map intentionally
            // re-states the strings so a constant value drift would surface as a test failure.
            ["金鑰狀態非 Active"] = (HttpStatusCode.Conflict, "INVALID_STATE_TRANSITION"),
            ["暫停操作僅限人為操作"] = (HttpStatusCode.UnprocessableEntity, "HUMAN_ACTOR_REQUIRED"),
            ["權限不足"] = (HttpStatusCode.Forbidden, "FORBIDDEN"),
            ["必須提供暫停原因"] = (HttpStatusCode.BadRequest, "VALIDATION_ERROR:reason_empty"),
        };

        var entry = map.First(kv => reason.StartsWith(kv.Key, StringComparison.Ordinal));
        var (expectedStatus, expectedErrorCode) = entry.Value;

        // RFC 9457 Problem Details wire contract (api-spec.md §2.2). Locks every failure scenario
        // that uses this step — including the @ignore'd ones, as they come online.
        ProblemAssertions.RequireProblem(_ctx.Response!, _ctx.ResponseBody!, expectedStatus, expectedErrorCode);
    }

    [Then(@"恢復失敗，錯誤原因為「(.*)」")]
    public void ThenResumeFailsWithReason(string reason)
    {
        var map = new Dictionary<string, (HttpStatusCode Status, string ErrorCode)>
        {
            // API wire contract — keep literals here to lock external HTTP error codes.
            // Production code uses *FailureCodes.* constants; this map intentionally
            // re-states the strings so a constant value drift would surface as a test failure.
            ["金鑰狀態非 Suspended"] = (HttpStatusCode.Conflict, "INVALID_STATE_TRANSITION"),
            ["權限不足"] = (HttpStatusCode.Forbidden, "FORBIDDEN"),
        };

        var entry = map.First(kv => reason.StartsWith(kv.Key, StringComparison.Ordinal));
        var (expectedStatus, expectedErrorCode) = entry.Value;

        // RFC 9457 Problem Details wire contract (api-spec.md §2.2). Locks every failure scenario
        // that uses this step — including the @ignore'd ones, as they come online.
        ProblemAssertions.RequireProblem(_ctx.Response!, _ctx.ResponseBody!, expectedStatus, expectedErrorCode);
    }
}
