using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ExpireKey;

// System Agent Job entry point (api-spec.md §3.4 marks C8 "❌ System Agent Job", no HTTP
// endpoint) — this handler IS the trigger surface, invoked directly via DI, not behind an
// endpoint. Mirrors CompleteGracePeriodScanHandler's comment: no HostedService/timer wrapper is
// built this round — no scenario asserts periodic scheduling; the first scheduled-consumer
// verification point is tracked at the orchestrator/checkpoint level.
//
// Shape differs from C9 on purpose: C9 has both a scan handler AND a per-key command handler
// (ICompleteGracePeriodHandler), because its Then steps assert a guard failure reachable via
// direct per-key invocation ("金鑰狀態非 Rotating"). C8's 06_ExpireKey.feature has no such
// direct-invocation scenario — its two negative scenarios ("尚未到期" / "已在終態") both assert
// the key is absent from the *scan result*, not a guarded per-key rejection. Filtering both the
// date and terminal-state predicates in the repository query (GetExpiredNonTerminalAsync)
// therefore fully satisfies every current scenario, and a per-key handler would be a zero-guard
// pass-through to ApiKey.Expire — not built, per "no scenario, no build" discipline
// (bdd-vertical-slice). detailed-design C8 does describe a per-key command model; if a future
// scenario needs direct per-key invocation (mirroring C9's guard-failure scenario), extract one
// then (mechanical refactor, not a redesign).
public class ExpireKeyScanHandler(
    IApiKeyRepository keyRepository,
    TimeProvider clock
) : IExpireKeyScanHandler
{
    public async Task<Result<ExpireKeyScanResponse, Failure>> HandleAsync(
        CancellationToken cancel = default)
    {
        var now = clock.GetUtcNow();

        var dueKeys = await keyRepository.GetExpiredNonTerminalAsync(now, cancel);

        var processedCount = 0;
        foreach (var key in dueKeys)
        {
            // Active → Expired only this round (ApiKey.Expire's doc comment carries the
            // Locked → Revoked deferral to scenario 46).
            key.Expire(now);
            await keyRepository.UpdateAsync(key, cancel);
            processedCount++;
        }

        // Always Ok (ExpireKeyScanResponse.cs) — zero transitions from an empty due set is a
        // successful scan, not a failure.
        return new ExpireKeyScanResponse(processedCount);
    }
}
