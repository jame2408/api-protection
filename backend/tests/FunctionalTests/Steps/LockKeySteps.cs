using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.LockKey;
using ApiKeyManagement.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class LockKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    // Fixed detection-rule evidence payload for this scenario (context-integration-spec.md
    // §4.6 I6 evidence is free-shape) — round-tripped verbatim into the KeyLocked event.
    private static readonly JsonElement Evidence = JsonSerializer.SerializeToElement(new
    {
        sourceIps = new[] { "203.0.113.9", "198.51.100.7" },
        distanceKm = 8000,
        windowMinutes = 5
    });

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"System 以 ruleId ""(.*)""、severity HIGH 鎖定 ""(.*)""，原因為「(.*)」")]
    public async Task WhenSystemLocksKey(string ruleId, string keyAlias, string reason)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        // Same token-issuance precedent as SuspendKeySteps.WhenSystemSuspendsKey.
        _ctx.AuthToken = TestTokenFactory.CreateSystemToken();
        _ctx.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ctx.AuthToken);

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/internal/keys/{keyId}/lock",
            new LockKeyEndpoint.Request(
                TenantId: _ctx.CurrentTenantId,
                RuleId: ruleId,
                Severity: "HIGH",
                Reason: reason,
                DetectedAt: DateTimeOffset.UtcNow,
                Evidence: Evidence));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"System 對 ""(.*)"" 發出鎖定命令")]
    public async Task WhenSystemIssuesLockCommand(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        _ctx.AuthToken = TestTokenFactory.CreateSystemToken();
        _ctx.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ctx.AuthToken);

        // Legitimate body + Suspended seed on purpose (mirrors SuspendKeySteps.WhenSystemSuspendsKey):
        // proves the rejection comes from the status guard, not from a malformed request.
        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/internal/keys/{keyId}/lock",
            new LockKeyEndpoint.Request(
                TenantId: _ctx.CurrentTenantId,
                RuleId: "impossible-travel",
                Severity: "HIGH",
                Reason: "異地同時存取",
                DetectedAt: DateTimeOffset.UtcNow,
                Evidence: Evidence));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"Security Admin（人為操作者）對 ""(.*)"" 發出鎖定命令")]
    public async Task WhenSecurityAdminIssuesLockCommand(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        _ctx.AuthToken = TestTokenFactory.CreateSecurityAdminToken();
        _ctx.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ctx.AuthToken);

        // Legitimate body + Active seed on purpose (mirrors WhenSystemIssuesLockCommand):
        // proves the rejection comes from the System-only role policy, not from a
        // malformed request or the status guard.
        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/internal/keys/{keyId}/lock",
            new LockKeyEndpoint.Request(
                TenantId: _ctx.CurrentTenantId,
                RuleId: "impossible-travel",
                Severity: "HIGH",
                Reason: "異地同時存取",
                DetectedAt: DateTimeOffset.UtcNow,
                Evidence: Evidence));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"Security Admin 對 ""(.*)"" 發出解鎖命令")]
    public async Task WhenSecurityAdminUnlocksKey(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        // Token already issued by the "操作者為 Security Admin" Given step above — do not
        // re-issue it. api-spec.md §3.2.7: POST /unlock has no request body (mirrors
        // SuspendKeySteps.WhenOperatorResumesKey).
        _ctx.Response = await _ctx.Client.PostAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/unlock", null);

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    [Then(@"鎖定失敗，錯誤原因為「(.*)」")]
    public void ThenLockFailsWithReason(string reason)
    {
        var map = new Dictionary<string, (HttpStatusCode Status, string ErrorCode)>
        {
            // API wire contract — keep literals here to lock external HTTP error codes.
            // Production code uses *FailureCodes.* constants; this map intentionally
            // re-states the strings so a constant value drift would surface as a test failure.
            ["金鑰狀態非 Active"] = (HttpStatusCode.Conflict, "INVALID_STATE_TRANSITION"),
            // System-only role policy (403 FORBIDDEN via ProblemAuthorizationResultHandler) —
            // decided 2026-07-12: endpoint-level RequireRole, not a handler actor guard.
            ["只有系統可以鎖定金鑰"] = (HttpStatusCode.Forbidden, "FORBIDDEN"),
        };

        var entry = map.First(kv => reason.StartsWith(kv.Key, StringComparison.Ordinal));
        var (expectedStatus, expectedErrorCode) = entry.Value;

        // RFC 9457 Problem Details wire contract (api-spec.md §2.2).
        ProblemAssertions.RequireProblem(_ctx.Response!, _ctx.ResponseBody!, expectedStatus, expectedErrorCode);
    }

    [Then(@"""(.*)"" 狀態變為 Locked")]
    public void ThenKeyStatusBecomesLocked(string keyAlias)
    {
        _ctx.Response!.StatusCode.Should().Be(HttpStatusCode.OK);

        // ADR-006: assert raw JSON literal to lock the wire-format string, not just the
        // round-tripped enum value. LockKeyResponse has no lifecycleStatus field (faithful I6
        // output) — the field under test here is previousStatus.
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("previousStatus").GetString().Should().Be("Active");
        doc.RootElement.TryGetProperty("lockedAt", out var lockedAt).Should().BeTrue();
        lockedAt.GetDateTimeOffset().Should().NotBe(default);

        var reloadedKeyId = _ctx.SeededKeys[keyAlias];

        // AsNoTracking: the Given step above added and saved this same entity on this same
        // scenario-scoped DbContext, so a tracked re-query would return the stale in-memory
        // instance (EF Core identity resolution) instead of reflecting the update made by the
        // HTTP request's own (separate) DbContext instance.
        var reloaded = Db.ApiKeys.AsNoTracking().Single(k => k.Id == reloadedKeyId);
        reloaded.Status.Should().Be(ApiKeyStatus.Locked, "the transition must be persisted, not just returned on the wire");
    }

    [Then(@"系統產生 KeyLocked 事件，包含 keyId、ruleId、reason、evidence")]
    public void ThenKeyLockedEventIsPublished()
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var keyId = doc.RootElement.GetProperty("keyId").GetGuid();

        using var payload = Db.RequireOutboxEvent("KeyLocked", keyId);
        var root = payload.RootElement;

        root.GetProperty("keyId").GetGuid().Should().Be(keyId);
        root.GetProperty("ruleId").GetString().Should().Be("impossible-travel");
        root.GetProperty("reason").GetString().Should().Be("異地同時存取");

        var evidence = root.GetProperty("evidence");
        var sourceIps = evidence.GetProperty("sourceIps");
        sourceIps.GetArrayLength().Should().Be(2);
        sourceIps[0].GetString().Should().Be("203.0.113.9");
        sourceIps[1].GetString().Should().Be("198.51.100.7");
        evidence.GetProperty("distanceKm").GetInt32().Should().Be(8000);
        evidence.GetProperty("windowMinutes").GetInt32().Should().Be(5);
    }

    [Then(@"系統產生 KeyUnlocked 事件，包含 keyId、unlockedBy")]
    public void ThenKeyUnlockedEventIsPublished()
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var keyId = doc.RootElement.GetProperty("keyId").GetGuid();

        using var payload = Db.RequireOutboxEvent("KeyUnlocked", keyId);
        var root = payload.RootElement;

        root.GetProperty("keyId").GetGuid().Should().Be(keyId);

        // unlockedBy is a nested Actor object on the wire (integration spec §6.1 / §3 Actor
        // schema) — distinct from the response's flat `unlockedBy` string (api-spec.md §3.2.7).
        var unlockedBy = root.GetProperty("unlockedBy");
        unlockedBy.GetProperty("type").GetString().Should().Be("User");
        unlockedBy.GetProperty("id").GetString().Should().Be("security-admin-1");
        unlockedBy.GetProperty("name").GetString().Should().Be("security-admin-1");

        // KeyUnlocked has no Reason field (mirrors KeyResumed) — lock the wire shape so a
        // future edit doesn't silently reintroduce one.
        root.TryGetProperty("reason", out _).Should().BeFalse();
    }
}
