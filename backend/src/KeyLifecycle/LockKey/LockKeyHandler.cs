using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.LockKey;

public class LockKeyHandler(
    IApiKeyRepository keyRepository
) : ILockKeyHandler
{
    public async Task<Result<LockKeyResponse, Failure>> HandleAsync(
        LockKeyCommand command, CancellationToken cancel = default)
    {
        // 1. Guard: key exists (tenantId + keyId).
        var apiKey = await keyRepository.GetByIdAsync(command.KeyId, command.TenantId, cancel);
        if (apiKey is null)
            return FailureProvider.CreateFailure(LockKeyFailureCodes.KeyNotFound);

        // 2. Guard: must be Active. I6 §4.6 distinguishes ALREADY_LOCKED / ALREADY_SUSPENDED /
        // TERMINAL sub-codes; collapsed here to a single code to match the scenario wording
        // ("金鑰狀態非 Active") — split into sub-codes if a future scenario needs it.
        if (apiKey.Status != ApiKeyStatus.Active)
            return FailureProvider.CreateFailure(LockKeyFailureCodes.InvalidStateTransition);

        // 3. Transition, persist. previousStatus captured before mutation; guard 2 guarantees
        // it is always Active here.
        var previousStatus = apiKey.Status;
        apiKey.Lock(command.RuleId, command.Reason, command.Evidence);
        await keyRepository.UpdateAsync(apiKey, cancel);

        return new LockKeyResponse(
            KeyId: apiKey.Id,
            PreviousStatus: previousStatus,
            LockedAt: DateTimeOffset.UtcNow);
    }
}
