using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

// System Agent Job entry point (api-spec.md §3.1 does not expose this BC as HTTP; §3.4 matrix
// marks it "❌ System Agent Job") — this handler IS the trigger surface, invoked directly via DI,
// not behind an endpoint. No HostedService/timer wrapper is built this round: no scenario
// asserts periodic scheduling; the first scheduled-consumer verification point is tracked at the
// orchestrator/checkpoint level.
public class CompleteGracePeriodScanHandler(
    IApiKeyRepository keyRepository,
    ICompleteGracePeriodHandler completeGracePeriodHandler
) : ICompleteGracePeriodScanHandler
{
    public async Task<Result<CompleteGracePeriodScanResponse, Failure>> HandleAsync(
        CancellationToken cancel = default)
    {
        // Query filters by Status only — the deadline predicate lives solely in the per-key
        // handler's guard (deferred this round, CompleteGracePeriodHandler.cs), so this scan
        // currently delegates every Rotating key it finds.
        var rotatingKeys = await keyRepository.GetRotatingAsync(cancel);

        var completedCount = 0;
        foreach (var key in rotatingKeys)
        {
            var result = await completeGracePeriodHandler.HandleAsync(
                new CompleteGracePeriodCommand(key.Id, key.TenantId), cancel);

            if (result.IsSuccess)
                completedCount++;
        }

        // Always Ok (CompleteGracePeriodScanResponse.cs) — zero completions from an empty or
        // all-guarded-out Rotating set is a successful scan, not a failure.
        return new CompleteGracePeriodScanResponse(completedCount);
    }
}
