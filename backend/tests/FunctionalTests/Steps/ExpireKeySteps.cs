using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.ExpireKey;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class ExpireKeySteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    // Regex pattern (the quoted alias is a "(.*)" capture group, so Reqnroll's
    // CucumberExpressionDetector classifies the whole attribute as Regex — lesson
    // 20260712-reqnroll-plus-escaping-depends-on-pattern-kind.md's judging method; the literal
    // text has no regex-special characters, so no escaping is needed here).
    //
    // Directly mirrors CompleteGracePeriodSteps.SetGraceDeadlineRelativeToNowAsync (same
    // technique — CurrentValue bypass on a tracked re-query, offset from the frozen DI
    // TimeProvider "now" — applied to ExpiresAt instead of GraceDeadline). "已超過" is
    // mechanically defined as ExpiresAt sitting before that frozen "now".
    [Given(@"當前時間已超過 ""(.*)"" 的 expiresAt")]
    public async Task GivenCurrentTimeIsPastExpiresAt(string keyAlias)
    {
        var now = _ctx.ServiceScope!.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        var keyId = _ctx.SeededKeys[keyAlias];

        var key = await Db.ApiKeys.SingleAsync(k => k.Id == keyId);
        Db.Entry(key).Property(k => k.ExpiresAt).CurrentValue = now.AddHours(-1);

        await Db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"System Agent 執行到期掃描")]
    public async Task WhenSystemAgentRunsExpiryScan()
    {
        // C8 has no HTTP endpoint (api-spec.md §3.4 matrix: System Agent Job) — DI direct
        // invocation IS the trigger surface, mirrors
        // CompleteGracePeriodSteps.WhenSystemAgentRunsGracePeriodScan. A fresh scope (rather than
        // reusing _ctx.ServiceScope) mirrors how the production job would resolve the handler
        // each run; _ctx.Response stays null throughout, since there is no HTTP wire for this
        // scenario. No HostedService/timer wrapper exists this round — this step calls the scan
        // handler directly.
        using var scope = _ctx.ServiceScope!.ServiceProvider
            .GetRequiredService<IServiceScopeFactory>().CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IExpireKeyScanHandler>();

        var result = await handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    // Regex pattern (the quoted alias is a "(.*)" capture group, so Reqnroll's
    // CucumberExpressionDetector classifies the whole attribute as Regex — lesson
    // 20260712-reqnroll-plus-escaping-depends-on-pattern-kind.md's judging method; the literal
    // text has no regex-special characters, so no escaping is needed here).
    [Then(@"""(.*)"" 狀態變為 Expired")]
    public async Task ThenKeyStatusBecomesExpired(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        var key = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == keyId);

        key.Status.Should().Be(ApiKeyStatus.Expired, "到期掃描應把已過期的 Active 金鑰轉為 Expired");
    }

    // Regex pattern (the trailing previousStatus is a "(.*)" capture group, so Reqnroll's
    // CucumberExpressionDetector classifies the whole attribute as Regex — same judging method as
    // above). Cannot mirror RevokeKeySteps.ThenKeyRevokedEventIsPublished's _ctx.ResponseBody
    // read — this scenario has no HTTP wire (WhenSystemAgentRunsExpiryScan's comment above),
    // ResponseBody is null throughout — so keyId is read from _ctx.SeededKeys instead. No alias
    // parameter in the step text — this step is currently this scenario's sole caller, so the
    // "key-A" alias literal below is hardcoded rather than parameterized, mirroring
    // CompleteGracePeriodSteps.ThenRotationLinkIsCleared's hardcode convention (revisit if/when a
    // second caller appears).
    [Then(@"系統產生 KeyExpired 事件，previousStatus 為 (.*)")]
    public void ThenKeyExpiredEventIsPublished(string previousStatus)
    {
        var keyId = _ctx.SeededKeys["key-A"];

        using var payload = Db.RequireOutboxEvent("KeyExpired", keyId);
        payload.RootElement.GetProperty("keyId").GetGuid().Should().Be(keyId);
        payload.RootElement.GetProperty("previousStatus").GetString().Should().Be(previousStatus);
    }
}
