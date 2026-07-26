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

    // C8 Locked→Revoked scenario: the ruleId given alongside the Locked seed, held between the
    // Given and Then steps — mirrors RevokeKeySteps._leakedPrefix's binding-class-field
    // convention for carrying a value across steps that isn't itself a keyAlias.
    private string? _lockRuleId;

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

    // Regex pattern (the quoted alias and ruleId are both "(.*)" capture groups, so Reqnroll's
    // CucumberExpressionDetector classifies the whole attribute as Regex — same judging method
    // as the sibling Given above; neither literal has regex-special characters, so no escaping
    // is needed). Differs from RevokeKeySteps.GivenKeyIsLocked (which seeds Locked without a
    // ruleId) precisely because this scenario needs LockRuleId populated to exercise C8's
    // Locked→Revoked reason string — GivenKeyIsLocked has no reason to carry one.
    [Given(@"金鑰 ""(.*)"" 狀態為 Locked，原始鎖定 ruleId 為 ""(.*)""")]
    public async Task GivenKeyIsLockedWithRuleId(string keyAlias, string ruleId)
    {
        _ctx.CurrentTenantId = "tenant-A";

        var key = _ctx.AddSeedKey(keyAlias);

        // Not key.Lock(ruleId, ...): Lock() also raises a KeyLocked domain event into the
        // outbox, which this scenario's seed does not intend (it seeds a key that is already
        // Locked, not one being locked as part of the scenario). Status and LockRuleId are set
        // directly via CurrentValue instead, bypassing the private setters — same technique as
        // RevokeKeySteps.GivenKeyIsLocked.
        Db.Entry(key).Property(k => k.Status).CurrentValue = ApiKeyStatus.Locked;
        Db.Entry(key).Property(k => k.LockRuleId).CurrentValue = ruleId;

        _lockRuleId = ruleId;

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

    // Regex pattern (the quoted alias is a "(.*)" capture group, so Reqnroll's
    // CucumberExpressionDetector classifies the whole attribute as Regex — same judging method
    // as the Given steps above; the literal text, including the full-width parenthesis, has no
    // regex-special characters, so no escaping is needed). Does not reuse
    // RevokeKeySteps.ThenKeyStatusBecomesRevoked: the step text differs (the "（非 Expired）"
    // suffix breaks Reqnroll's implicit anchoring, so that step would never match this one
    // anyway), and that step's HTTP-wire branch is irrelevant here (C8 has no HTTP wire — see
    // WhenSystemAgentRunsExpiryScan's comment above). "（非 Expired）" needs no separate negative
    // assertion — Status.Should().Be(Revoked) already excludes Expired by equality.
    [Then(@"""(.*)"" 狀態變為 Revoked（非 Expired）")]
    public async Task ThenKeyStatusBecomesRevokedNotExpired(string keyAlias)
    {
        var keyId = _ctx.SeededKeys[keyAlias];

        var key = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == keyId);

        key.Status.Should().Be(ApiKeyStatus.Revoked, "C8 的 Locked→Revoked 分支必須保留安全上下文，而非落回 Expired");
    }

    // This scenario's sole Then assertion on the KeyRevoked payload — only the event's presence
    // and reason-contains-ruleId, per the step text. previousStatus / revokedBy are not asserted
    // here on purpose; they are outside this step's stated semantics (see Refactor-assessment
    // trailer for whether a follow-up assertion step is worth adding).
    [Then(@"系統產生 KeyRevoked 事件，reason 包含原始鎖定 ruleId")]
    public void ThenKeyRevokedEventReasonContainsLockRuleId()
    {
        var keyId = _ctx.SeededKeys["key-A"];

        using var payload = Db.RequireOutboxEvent("KeyRevoked", keyId);
        payload.RootElement.GetProperty("reason").GetString().Should()
            .Contain(_lockRuleId!, "reason 必須包含原始鎖定的 ruleId 以保留安全上下文");
    }
}
