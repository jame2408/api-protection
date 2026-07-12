using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

public class CompleteGracePeriodHandler(
    IApiKeyRepository keyRepository,
    TimeProvider clock
) : ICompleteGracePeriodHandler
{
    public async Task<Result<CompleteGracePeriodResponse, Failure>> HandleAsync(
        CompleteGracePeriodCommand command, CancellationToken cancel = default)
    {
        // 1. Guard: key exists (tenantId + keyId).
        var key = await keyRepository.GetByIdAsync(command.KeyId, command.TenantId, cancel);
        if (key is null)
            return FailureProvider.CreateFailure(CompleteGracePeriodFailureCodes.KeyNotFound);

        // 2. Guard: status = Rotating.
        if (key.Status != ApiKeyStatus.Rotating)
            return FailureProvider.CreateFailure(CompleteGracePeriodFailureCodes.InvalidStateTransition);

        // 3. Guard: now >= GraceDeadline — deliberately NOT implemented this round (BDD
        //    discipline: only one @ignore removed at a time). "寬限期尚未到期 — 不處理" is the
        //    next scenario in the backlog and will red-drive this guard; insertion point is here,
        //    between the status guard above and the state transition below, mirroring
        //    RotateKeyHandler's guard-order precedent for its analogous "expired" check.

        // INV-2 guarantees SuccessorKeyId is non-null while Status is Rotating (ApiKey.cs
        // CompleteGracePeriod doc comment) — captured before the domain method clears it, since
        // the response needs it.
        var successorKeyId = key.SuccessorKeyId!.Value;

        key.CompleteGracePeriod(clock.GetUtcNow());

        await keyRepository.UpdateAsync(key, cancel);

        // detailed-design C9 side effect "清除雙向關聯" — CompleteGracePeriod only clears this
        // key's own SuccessorKeyId; the successor's PredecessorKeyId is the other half, cleared
        // here the same way RevokeKeyHandler clears it for its analogous Rotating → Revoked
        // transition (RevokeKeyHandler.cs step 5, design-doc.md T6). A missing successor points at
        // data corruption outside this scenario's scope, so we skip silently rather than fail the
        // completion that already succeeded.
        var successor = await keyRepository.GetByIdAsync(successorKeyId, command.TenantId, cancel);
        if (successor is not null)
        {
            successor.ClearPredecessorLink();
            await keyRepository.UpdateAsync(successor, cancel);
        }

        return new CompleteGracePeriodResponse(key.Id, successorKeyId);
    }
}
