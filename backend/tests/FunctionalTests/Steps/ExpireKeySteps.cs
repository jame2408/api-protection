using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.ExpireKey;
using ApiKeyManagement.SharedKernel.Domain;
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

    // C8 47/48 scenario ("金鑰尚未到期 — 不處理"): the scan Result, held between the When and
    // Then steps so the Then can read ProcessedCount — mirrors
    // CompleteGracePeriodSteps._completeGracePeriodResult's binding-class-field bridging
    // convention (this slice has no HTTP endpoint, so _ctx.Response can't carry it). Storing the
    // whole Result (not just ExpireKeyScanResponse) rather than unwrapping in the When, so the
    // existing `result.IsSuccess.Should().BeTrue()` assertion there stays intact and this field
    // stays a pure carry-through.
    private Result<ExpireKeyScanResponse, Failure>? _scanResult;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given(@"當前時間已超過 ""(.*)"" 的 expiresAt")]
    public Task GivenCurrentTimeIsPastExpiresAt(string keyAlias) =>
        // "已超過" is mechanically defined as ExpiresAt sitting before the frozen "now" shared by
        // test and handler via DI TimeProvider (FrozenTimeProvider) — same reasoning as
        // CompleteGracePeriodSteps.GivenCurrentTimeIsPastGraceDeadline for GraceDeadline.
        SetExpiresAtRelativeToNowAsync(keyAlias, TimeSpan.FromHours(-1));

    // Regex pattern (the quoted alias is a "(.*)" capture group, so Reqnroll's
    // CucumberExpressionDetector classifies the whole attribute as Regex — lesson
    // 20260712-reqnroll-plus-escaping-depends-on-pattern-kind.md's judging method; the literal
    // text has no regex-special characters, so no escaping is needed here).
    //
    // "尚未超過" does not lean on the seed default (GivenKeyIsActive's ExpiresAt = UtcNow +30
    // days is already in the future) — this Given still owns the "not yet past" semantics
    // explicitly via SetExpiresAtRelativeToNowAsync, so it stays correct even if that seed
    // default ever changes (C9 precedent: CompleteGracePeriodSteps.GivenCurrentTimeIsNotPast-
    // GraceDeadline is the same future-value variant that doesn't rely on a seed default either).
    [Given(@"當前時間尚未超過 ""(.*)"" 的 expiresAt")]
    public Task GivenCurrentTimeIsNotPastExpiresAt(string keyAlias) =>
        // "尚未超過" is the future-value mirror of "已超過" above — same mechanical definition,
        // opposite sign offset from frozen "now".
        SetExpiresAtRelativeToNowAsync(keyAlias, TimeSpan.FromHours(1));

    // Shared by the two Given steps above (extracted at their second occurrence in this file —
    // identical body except the offset sign, no differing seed/register semantics to justify
    // keeping them separate). Directly mirrors
    // CompleteGracePeriodSteps.SetGraceDeadlineRelativeToNowAsync (same technique — CurrentValue
    // bypass on a tracked re-query, offset from the frozen DI TimeProvider "now" — applied to
    // ExpiresAt instead of GraceDeadline).
    private async Task SetExpiresAtRelativeToNowAsync(string keyAlias, TimeSpan offsetFromNow)
    {
        var now = _ctx.ServiceScope!.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        var keyId = _ctx.SeededKeys[keyAlias];

        var key = await Db.ApiKeys.SingleAsync(k => k.Id == keyId);
        Db.Entry(key).Property(k => k.ExpiresAt).CurrentValue = now + offsetFromNow;

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

        // C8's first four scenarios only observe DB/outbox; the 47/48 scenario (and 48/48) also
        // need the scan result itself (ProcessedCount) to assert "not in the scan result" — stash
        // it into _scanResult for the Then step to read, mirroring
        // CompleteGracePeriodSteps._completeGracePeriodResult's field-bridging convention.
        _scanResult = result;
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

    // Regex pattern (the quoted alias is a "(.*)" capture group, so Reqnroll's
    // CucumberExpressionDetector classifies the whole attribute as Regex — lesson
    // 20260712-reqnroll-plus-escaping-depends-on-pattern-kind.md's judging method; the literal
    // text has no regex-special characters, so no escaping is needed here).
    //
    // "不在掃描結果中，狀態保持 Active" is mechanically defined as three assertions (mirrors
    // CompleteGracePeriodSteps.ThenKeyStatusStaysRotatingWithNoEvent's "no side effect" method):
    // (a) the scan Result's ProcessedCount is 0 — this is the discriminating assertion the first
    // four (positive-path) C8 scenarios don't need, since this scenario's DB holds exactly one
    // key (GivenKeyIsActive seeds only one), ProcessedCount == 0 precisely proves the date
    // predicate excluded it from the scan, not just that this one key's status happens to be
    // untouched; (b) DB status is untouched; (c) no outbox row besides the seed's KeyCreated row
    // landed (same robust-to-seed-count definition as the CompleteGracePeriodSteps precedent).
    //
    // _scanResult is a nullable Result<ExpireKeyScanResponse, Failure> — Nullable<T>.Value and
    // the Result struct's own .Value member share the same name, so the Nullable unwrap must
    // happen via a local variable first before reading the Result's .Value (readonly struct
    // member) — see this round's spec background note on the naming collision.
    [Then(@"""(.*)"" 不在掃描結果中，狀態保持 Active")]
    public async Task ThenKeyIsNotInScanResultAndStaysActive(string keyAlias)
    {
        var scanResult = _scanResult!.Value;
        scanResult.Value.ProcessedCount.Should().Be(0, "未到期的金鑰不應被掃描處理");

        var keyId = _ctx.SeededKeys[keyAlias];
        var key = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == keyId);
        key.Status.Should().Be(ApiKeyStatus.Active, "未到期的金鑰狀態應保持 Active");

        var hasNonSeedEvent = await Db.OutboxMessages.AnyAsync(m => m.EventType != "KeyCreated");
        hasNonSeedEvent.Should().BeFalse("未到期的金鑰掃描不應產生任何非 seed 事件");
    }
}
