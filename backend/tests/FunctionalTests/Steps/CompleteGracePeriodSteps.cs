using ApiKeyManagement.FunctionalTests.Infrastructure;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;
using ApiKeyManagement.KeyLifecycle.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace ApiKeyManagement.FunctionalTests.Steps;

[Binding]
public class CompleteGracePeriodSteps(FunctionalTestContext ctx)
{
    private readonly FunctionalTestContext _ctx = ctx;

    private AppDbContext Db =>
        _ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given(@"金鑰 ""(.*)"" 狀態為 Rotating")]
    public async Task GivenKeyStatusIsRotating(string keyAlias)
    {
        _ctx.CurrentTenantId = "tenant-A";

        // Successor is registered (unlike RotateKeySteps.GivenOtherRotatingKeyExists's anonymous
        // `register: false` successor) because this scenario's Then steps below assert against
        // the successor's own PredecessorKeyId, so it needs to be addressable by alias too.
        var key = _ctx.AddSeedKey(keyAlias);
        var successor = _ctx.AddSeedKey(keyAlias + "-successor");

        // ApiKey properties are all `private set`; bypass via CurrentValue as in
        // RevokeKeySteps.GivenKeyIsRotatingWithSuccessor. GraceDeadline is set by the next Given
        // step, not here.
        Db.Entry(key).Property(k => k.Status).CurrentValue = ApiKeyStatus.Rotating;
        Db.Entry(key).Property(k => k.SuccessorKeyId).CurrentValue = successor.Id;
        Db.Entry(successor).Property(k => k.PredecessorKeyId).CurrentValue = key.Id;

        await Db.SaveChangesAsync();
    }

    [Given(@"當前時間已超過 ""(.*)"" 的 graceDeadline")]
    public async Task GivenCurrentTimeIsPastGraceDeadline(string keyAlias)
    {
        // "已超過" is mechanically defined as GraceDeadline sitting before the frozen "now"
        // shared by test and handler via DI TimeProvider (FrozenTimeProvider) — same reasoning as
        // RotateKeySteps.GivenKeyIsActiveButExpired for ExpiresAt.
        var now = _ctx.ServiceScope!.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        var keyId = _ctx.SeededKeys[keyAlias];

        // Tracked re-query on the same scenario-scoped DbContext returns the same instance the
        // prior Given step added (EF Core identity resolution) — CurrentValue bypass mirrors it.
        var key = await Db.ApiKeys.SingleAsync(k => k.Id == keyId);
        Db.Entry(key).Property(k => k.GraceDeadline).CurrentValue = now.AddHours(-1);

        await Db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // When
    // -------------------------------------------------------------------------

    [When(@"System Agent 執行寬限期掃描")]
    public async Task WhenSystemAgentRunsGracePeriodScan()
    {
        // C9 has no HTTP endpoint (api-spec.md §3.1 / §3.4 matrix: System Agent Job) — DI direct
        // invocation IS the trigger surface. A fresh scope (rather than reusing _ctx.ServiceScope)
        // mirrors how the production job would resolve the handler each run; _ctx.Response stays
        // null throughout, since there is no HTTP wire for this scenario (see the null-Response
        // branches added to RevokeKeySteps.cs for the shared Then steps this scenario reuses). No
        // HostedService/timer wrapper exists this round — this step calls the scan handler
        // directly.
        using var scope = _ctx.ServiceScope!.ServiceProvider
            .GetRequiredService<IServiceScopeFactory>().CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICompleteGracePeriodScanHandler>();

        var result = await handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Then
    // -------------------------------------------------------------------------

    // Cucumber Expression (no "(.*)" capture group / quantifier-group in the pattern, so Reqnroll's
    // CucumberExpressionDetector does not classify this as Regex) — the "/" is one of the five
    // characters Cucumber Expression syntax requires escaping ({ } ( ) \ /), hence "\/" below
    // (lesson 20260712-reqnroll-plus-escaping-depends-on-pattern-kind.md's judging method applied
    // to "/" instead of "+"). No alias parameter in the step text — this step is currently this
    // scenario's sole caller, so the "key-A" / "key-A-successor" alias literals below are
    // hardcoded rather than parameterized (revisit if/when a second caller appears).
    [Then(@"清除 successorKeyId \/ predecessorKeyId 關聯")]
    public async Task ThenRotationLinkIsCleared()
    {
        var keyId = _ctx.SeededKeys["key-A"];
        var successorId = _ctx.SeededKeys["key-A-successor"];

        var key = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == keyId);
        var successor = await Db.ApiKeys.AsNoTracking().SingleAsync(k => k.Id == successorId);

        key.SuccessorKeyId.Should().BeNull();
        successor.PredecessorKeyId.Should().BeNull();
    }

    [Then(@"系統產生 KeyGracePeriodExpired 事件，包含 keyId、successorKeyId")]
    public void ThenKeyGracePeriodExpiredEventIsPublished()
    {
        var keyId = _ctx.SeededKeys["key-A"];
        var successorId = _ctx.SeededKeys["key-A-successor"];

        using var payload = Db.RequireOutboxEvent("KeyGracePeriodExpired", keyId);
        payload.RootElement.GetProperty("keyId").GetGuid().Should().Be(keyId);
        payload.RootElement.GetProperty("successorKeyId").GetGuid().Should().Be(successorId);
    }
}
