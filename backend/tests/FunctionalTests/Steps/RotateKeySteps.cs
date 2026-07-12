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

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"Consumer 對 ""(.*)"" 發起輪替，寬限期為 24 小時")]
    public async Task WhenConsumerInitiatesRotationWith24HourGrace(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        _ctx.AuthToken = TestTokenFactory.CreateConsumerToken();
        _ctx.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ctx.AuthToken);

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            $"/api/v1/tenants/{_ctx.CurrentTenantId}/keys/{keyId}/rotate",
            new RotateKeyEndpoint.Request("PT24H"));

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
}
