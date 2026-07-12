using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.UnlockKey;

public class UnlockKeyHandler(
    IApiKeyRepository keyRepository
) : IUnlockKeyHandler
{
    public async Task<Result<UnlockKeyResponse, Failure>> HandleAsync(
        UnlockKeyCommand command, CancellationToken cancel = default)
    {
        // 1. Guard: key exists (tenantId + keyId).
        var apiKey = await keyRepository.GetByIdAsync(command.KeyId, command.TenantId, cancel);
        if (apiKey is null)
            return FailureProvider.CreateFailure(UnlockKeyFailureCodes.KeyNotFound);

        // 2. Guard: must be Locked
        if (apiKey.Status != ApiKeyStatus.Locked)
            return FailureProvider.CreateFailure(UnlockKeyFailureCodes.InvalidStateTransition);

        // 3. Transition, persist
        apiKey.Unlock(command.UnlockedBy);
        await keyRepository.UpdateAsync(apiKey, cancel);

        return new UnlockKeyResponse(
            KeyId: apiKey.Id,
            LifecycleStatus: apiKey.Status,
            UnlockedBy: command.UnlockedBy.Id);
    }
}
