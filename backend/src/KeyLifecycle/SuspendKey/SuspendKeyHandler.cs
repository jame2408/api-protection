using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.SuspendKey;

public class SuspendKeyHandler(
    IApiKeyRepository keyRepository
) : ISuspendKeyHandler
{
    public async Task<Result<SuspendKeyResponse, Failure>> HandleAsync(
        SuspendKeyCommand command, CancellationToken cancel = default)
    {
        // 1. Guard: suspend is human-actor-only (ADR-024 §2) — zero-I/O check, fail-fast before
        // any repository access.
        if (command.SuspendedBy.Type == ActorType.System)
            return FailureProvider.CreateFailure(SuspendKeyFailureCodes.HumanActorRequired);

        // 2. Guard: key exists (tenantId + keyId)
        var apiKey = await keyRepository.GetByIdAsync(command.KeyId, command.TenantId, cancel);
        if (apiKey is null)
            return FailureProvider.CreateFailure(SuspendKeyFailureCodes.KeyNotFound);

        // 3. Guard: reason required
        if (string.IsNullOrWhiteSpace(command.Reason))
            return FailureProvider.CreateFailure(SuspendKeyFailureCodes.ValidationErrorReasonEmpty);

        // 4. Guard: must be Active (INV-6)
        if (apiKey.Status != ApiKeyStatus.Active)
            return FailureProvider.CreateFailure(SuspendKeyFailureCodes.InvalidStateTransition);

        // 5. Transition, persist
        apiKey.Suspend(command.Reason, command.SuspendedBy);
        await keyRepository.UpdateAsync(apiKey, cancel);

        return new SuspendKeyResponse(
            KeyId: apiKey.Id,
            LifecycleStatus: apiKey.Status,
            SuspendedBy: command.SuspendedBy.Id,
            Reason: command.Reason);
    }
}
