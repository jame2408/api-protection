using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.RotateKey;
using ApiKeyManagement.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class RotateKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    private IApiKeyHasher Hasher =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<IApiKeyHasher>();

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given(@"同一 Consumer + Environment 下沒有其他 Rotating 金鑰")]
    public void GivenNoOtherRotatingKeyExists()
    {
        // Respawn resets the DB every scenario (TestHooks.cs AfterScenario), so no Rotating row
        // can exist at this point — asserted here as a documented positive precondition rather
        // than a silent no-op, matching the scenario's Given intent.
        Db.ApiKeys.Any(k => k.Status == ApiKeyStatus.Rotating).Should().BeFalse();
    }

    [Given(@"金鑰 ""(.*)"" 狀態為 Active，且屬於其他 Consumer")]
    public async Task GivenKeyIsActiveOwnedByOtherConsumer(string keyAlias)
    {
        _ctx.CurrentTenantId = "tenant-A";

        // "other-consumer" is deliberately different from the "操作者為一般 Consumer" When
        // step's token consumerId claim ("consumer-1") — that mismatch is the mechanical
        // definition of "非自身金鑰" this scenario asserts against (mirrors
        // RevokeKeySteps.CreateSeedKey's seed shape, private there so re-declared here).
        var (key, _) = ApiKey.Create(
            consumerId: "other-consumer",
            tenantId: _ctx.CurrentTenantId,
            name: keyAlias,
            environment: "Production",
            scopes: ["seed:read"],
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            policyId: Guid.NewGuid(),
            hasher: Hasher);

        Db.ApiKeys.Add(key);
        _ctx.SeededKeys[keyAlias] = key.Id;

        await Db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"Consumer 對 ""(.*)"" 發起輪替，寬限期為 24 小時")]
    public async Task WhenConsumerInitiatesRotationWith24HourGrace(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        // This When's semantics are "the key's own Consumer initiates rotation" — the token's
        // consumerId claim must match the seed's ConsumerId (RevokeKeySteps.CreateSeedKey:
        // "any-consumer"), otherwise the ownership guard (RotateKeyHandler) rejects it as
        // non-self rotation.
        _ctx.AuthToken = TestTokenFactory.CreateConsumerToken(consumerId: "any-consumer");
        _ctx.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ctx.AuthToken);

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/rotate",
            new RotateKeyEndpoint.Request("PT24H"));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    [When(@"操作者對 ""(.*)"" 發起輪替")]
    public async Task WhenOperatorInitiatesRotation(string keyAlias)
    {
        // Token already issued by the Given step above — do not re-issue it, this step must
        // work for whichever actor the Given set up (mirrors LockKeySteps.WhenOperatorUnlocksKey).
        // Legitimate body (null grace period = default grace path) + Active seed on purpose:
        // proves the rejection comes from the role policy, not from a malformed request or the
        // status guard.
        var keyId = _ctx.SeededKeys[keyAlias];

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/rotate",
            new RotateKeyEndpoint.Request(null));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    [Then(@"""(.*)"" 狀態變為 Rotating")]
    public async Task ThenKeyStatusBecomesRotating(string keyAlias)
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("oldKey").GetProperty("lifecycleStatus").GetString()
            .Should().Be("Rotating");

        var keyId = _ctx.SeededKeys[keyAlias];
        var dbKey = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == keyId);
        dbKey.Status.Should().Be(ApiKeyStatus.Rotating);
    }

    [Then(@"系統建立新金鑰 ""(.*)""，狀態為 Active")]
    public async Task ThenNewKeyIsCreatedActive(string keyAlias)
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var newKey = doc.RootElement.GetProperty("newKey");
        newKey.GetProperty("lifecycleStatus").GetString().Should().Be("Active");

        var keyId = newKey.GetProperty("keyId").GetGuid();
        _ctx.SeededKeys[keyAlias] = keyId;

        var dbKey = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == keyId);
        dbKey.Status.Should().Be(ApiKeyStatus.Active);
    }

    [Then(@"""(.*)""\.successorKeyId 指向 ""(.*)""")]
    public async Task ThenSuccessorKeyIdPointsTo(string predecessorAlias, string successorAlias)
    {
        var successorId = _ctx.SeededKeys[successorAlias];

        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("oldKey").GetProperty("successorKeyId").GetGuid()
            .Should().Be(successorId);

        var predecessorId = _ctx.SeededKeys[predecessorAlias];
        var dbKey = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == predecessorId);
        dbKey.SuccessorKeyId.Should().Be(successorId);
    }

    [Then(@"""(.*)""\.predecessorKeyId 指向 ""(.*)""")]
    public async Task ThenPredecessorKeyIdPointsTo(string successorAlias, string predecessorAlias)
    {
        var predecessorId = _ctx.SeededKeys[predecessorAlias];

        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("newKey").GetProperty("predecessorKeyId").GetGuid()
            .Should().Be(predecessorId);

        var successorId = _ctx.SeededKeys[successorAlias];
        var dbKey = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == successorId);
        dbKey.PredecessorKeyId.Should().Be(predecessorId);
    }

    [Then(@"""(.*)""\.graceDeadline 設為 24 小時後")]
    public async Task ThenGraceDeadlineIsSet24HoursLater(string keyAlias)
    {
        // FrozenTimeProvider (ApiKeyManagementWebApplicationFactory) shares one deterministic
        // "now" between the test and the handler's guard, so this equality is exact.
        var now = _ctx.ServiceScope!.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var expected = now + TimeSpan.FromHours(24);

        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("oldKey").GetProperty("graceDeadline").GetDateTimeOffset()
            .Should().Be(expected);

        var keyId = _ctx.SeededKeys[keyAlias];
        var dbKey = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == keyId);
        dbKey.GraceDeadline.Should().Be(expected);
    }

    [Then(@"系統產生 KeyRotationInitiated 事件，包含 oldKeyId、newKeyId、graceDeadline")]
    public void ThenKeyRotationInitiatedEventIsPublished()
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var oldKeyId = doc.RootElement.GetProperty("oldKey").GetProperty("keyId").GetGuid();
        var newKeyId = doc.RootElement.GetProperty("newKey").GetProperty("keyId").GetGuid();
        var graceDeadline = doc.RootElement.GetProperty("oldKey").GetProperty("graceDeadline")
            .GetDateTimeOffset();

        using var payload = Db.RequireOutboxEvent("KeyRotationInitiated", oldKeyId);
        var root = payload.RootElement;
        root.GetProperty("oldKeyId").GetGuid().Should().Be(oldKeyId);
        root.GetProperty("newKeyId").GetGuid().Should().Be(newKeyId);
        root.GetProperty("graceDeadline").GetDateTimeOffset().Should().Be(graceDeadline);
    }

    [Then(@"系統回傳 ""(.*)"" 的金鑰明文（Display Once）")]
    public void ThenRawKeyIsReturned(string keyAlias)
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var newKey = doc.RootElement.GetProperty("newKey");

        var rawKey = newKey.GetProperty("rawKey").GetString();
        rawKey.Should().NotBeNullOrEmpty();

        // Key B inherits environment from Key A (tenant-A / Production seed), so KeyPrefix is a
        // deterministic wire value: apk_{first 4 chars of tenantId, lowercased}_{env abbr} =
        // apk_tena_prod (CreateApiKeySteps.cs ThenRawKeyIsReturned precedent).
        var keyPrefix = newKey.GetProperty("keyPrefix").GetString();
        keyPrefix.Should().Be("apk_tena_prod");
        rawKey.Should().StartWith(keyPrefix + "_");

        // truncatedKey: display-safe suffix "..." + last 4 of rawKey (api-spec.md §2.2).
        var truncatedKey = newKey.GetProperty("truncatedKey").GetString();
        truncatedKey.Should().MatchRegex(@"^\.\.\..{4}$");
        truncatedKey.Should().Be("..." + rawKey![^4..]);
    }

    [Then(@"同一交易內為 ""(.*)"" 建立預設 AccessPolicy")]
    public async Task ThenDefaultAccessPolicyIsCreated(string keyAlias)
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var policyId = doc.RootElement.GetProperty("newKey").GetProperty("policyId").GetGuid();

        var exists = await Db.AccessPolicies.AnyAsync(p => p.Id == policyId);
        exists.Should().BeTrue();
    }

    [Then(@"輪替失敗，錯誤原因為「(.*)」")]
    public void ThenRotateFailsWithReason(string reason)
    {
        var map = new Dictionary<string, (HttpStatusCode Status, string ErrorCode)>
        {
            // API wire contract — keep literals here to lock external HTTP error codes.
            // Production code uses *FailureCodes.* constants; this map intentionally
            // re-states the strings so a constant value drift would surface as a test failure.
            // TenantAdmin/Consumer role policy (403 FORBIDDEN via ProblemAuthorizationResultHandler)
            // — §3.2.4 Errors 表已補列（本 commit）。
            ["權限不足"] = (HttpStatusCode.Forbidden, "FORBIDDEN"),
            // §3.2.4 Errors 表其餘三碼（INVALID_STATE_TRANSITION／ROTATION_IN_PROGRESS／
            // KEY_ALREADY_EXPIRED）由對應場景啟用輪逐條補入此表。
        };

        var entry = map.First(kv => reason.StartsWith(kv.Key, StringComparison.Ordinal));
        var (expectedStatus, expectedErrorCode) = entry.Value;

        // RFC 9457 Problem Details wire contract (api-spec.md §2.2).
        ProblemAssertions.RequireProblem(_ctx.Response!, _ctx.ResponseBody!, expectedStatus, expectedErrorCode);
    }
}
