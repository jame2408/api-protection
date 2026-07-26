using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.ValidateKey;
using ApiKeyManagement.TestInfrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class ValidateKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    // "一個前綴不合法的金鑰字串" Given 到 When 之間的暫存 — 本場景不 seed 任何金鑰，沒有
    // SeededRawKeys 可查（RevokeKeySteps._leakedPrefix 同型先例）。
    private string? _invalidPrefixKeyString;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given(@"""(.*)"" 的 scopes 為 \[(.*)\]")]
    public async Task GivenKeyHasScopes(string keyAlias, string scopesRaw)
    {
        // Split/trim/unquote the raw bracket contents rather than a fixed-arity Cucumber
        // Expression pattern (Reqnroll's own suggestion for the redA log was hardcoded to
        // exactly 2 items): sibling @ignore'd scenarios in this same feature reuse this Given
        // with a 1-item list (line 25 "Rotating 金鑰在寬限期內仍可驗證") and a 2-item list
        // (line 17, this scenario) — a fixed {string},{string} pattern would only match the
        // latter.
        var scopes = scopesRaw
            .Split(',')
            .Select(s => s.Trim().Trim('"'))
            .ToArray();

        // Must mutate the entity the preceding "狀態為" Given already seeded, not call
        // AddSeedKey a second time under the same alias: SeededKeys/SeededRawKeys are Dictionary
        // indexers, so a second AddSeedKey call would simply repoint them at a brand-new row —
        // discarding whatever state/relations the first Given already set up (e.g. the Rotating
        // 金鑰在寬限期內仍可驗證 scenario sets Status=Rotating plus a successor back-reference on
        // the first row; a second seed leaves that row an orphan and re-points the alias at a
        // fresh Active-status row, so the scenario would validate the wrong key under the right
        // premise's name — the vacuous-green shape in lesson
        // 20260705-vacuous-green-scenario.md). Looking the id up via SeededKeys and re-fetching
        // as a tracked entity keeps this Given composable with whichever prior Given ran.
        var key = await Db.ApiKeys.SingleAsync(k => k.Id == _ctx.SeededKeys[keyAlias]);
        Db.Entry(key).Property(k => k.Scopes).CurrentValue = scopes;

        await Db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    // 本場景不 seed 任何金鑰（07_ValidateKey.feature 場景註解）：Layer 1 是純字串前綴檢查，
    // 不需要任何已存在的 ApiKey 列。任何不以 "apk_" 起頭的字串都合乎「前綴不合法」的前提，
    // 固定值即可、無需隨機化。
    [Given(@"一個前綴不合法的金鑰字串")]
    public void GivenAKeyStringWithInvalidPrefix()
    {
        _invalidPrefixKeyString = "not_a_valid_prefix_key_string";
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"Gateway 以 ""(.*)"" 的完整金鑰、來源 IP ""(.*)""、requestedScope ""(.*)"" 呼叫驗證")]
    public async Task WhenGatewayCallsValidate(string keyAlias, string sourceIp, string requestedScope)
    {
        // Gateway-as-caller (Data Plane, api-spec.md §4) authenticates as the System role —
        // same token-issuance precedent as LockKeySteps.WhenSystemLocksKey.
        _ctx.AuthenticateAs(TestTokenFactory.CreateSystemToken());

        var rawKey = _ctx.SeededRawKeys[keyAlias];

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            ValidateKeyEndpoint.Route,
            new ValidateKeyEndpoint.Request(
                ApiKey: rawKey,
                SourceIp: sourceIp,
                RequestedScope: requestedScope,
                RequestId: null));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // Distinct step text from WhenGatewayCallsValidate ("以 "(.*)" 的完整金鑰" points at a
    // seeded alias) — this scenario has no seed to alias, it sends the raw Given string as-is
    // (07_ValidateKey.feature "金鑰格式不合法" 場景).
    [When(@"Gateway 以該金鑰字串、來源 IP ""(.*)""、requestedScope ""(.*)"" 呼叫驗證")]
    public async Task WhenGatewayCallsValidateWithInvalidPrefixString(string sourceIp, string requestedScope)
    {
        _ctx.AuthenticateAs(TestTokenFactory.CreateSystemToken());

        _ctx.Response = await _ctx.Client.PostAsJsonAsync(
            ValidateKeyEndpoint.Route,
            new ValidateKeyEndpoint.Request(
                ApiKey: _invalidPrefixKeyString!,
                SourceIp: sourceIp,
                RequestedScope: requestedScope,
                RequestId: null));

        _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    [Then(@"驗證通過，valid 為 true")]
    public void ThenValidationSucceeds()
    {
        _ctx.Response!.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        doc.RootElement.GetProperty("valid").GetBoolean().Should().BeTrue();
    }

    // 扁平失敗 body（2026-07-26 裁決，非 RFC 9457）——ProblemAssertions 不適用，直接讀
    // JsonDocument 欄位，比照 RevokeKeySteps 讀 _ctx.ResponseBody 的既有寫法。
    [Then(@"驗證失敗，errorCode 為 ""(.*)""，httpStatusHint 為 (.*)")]
    public void ThenValidationFails(string expectedErrorCode, int expectedHttpStatusHint)
    {
        _ctx.Response!.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var root = doc.RootElement;

        root.GetProperty("valid").GetBoolean().Should().BeFalse();
        root.GetProperty("errorCode").GetString().Should().Be(expectedErrorCode);
        root.GetProperty("httpStatusHint").GetInt32().Should().Be(expectedHttpStatusHint);
    }

    [Then(@"回應包含 keyId、tenantId、consumerId、environment、scopes")]
    public void ThenResponseContainsMetadata()
    {
        using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
        var root = doc.RootElement;

        // Locks the actual wire values (ADR-006 convention), not just field presence — this
        // scenario seeds exactly one alias, so the sole SeededKeys entry is the expected match.
        root.GetProperty("keyId").GetGuid().Should().Be(_ctx.SeededKeys.Values.Single());
        root.GetProperty("tenantId").GetString().Should().Be(_ctx.CurrentTenantId);
        root.GetProperty("consumerId").GetString().Should().Be("any-consumer");
        root.GetProperty("environment").GetString().Should().Be("Production");
        root.GetProperty("scopes").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["orders:read", "orders:write"]);

        // rateLimitConfig deliberately absent — 07_ValidateKey.feature scenario comment / this
        // scenario's own scope: collapsed to Wave 9 (ADR-029 does not regulate AccessPolicy
        // fields).
        root.TryGetProperty("rateLimitConfig", out _).Should().BeFalse();
    }
}
