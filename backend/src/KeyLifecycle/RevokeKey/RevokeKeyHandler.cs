using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeKey;

public class RevokeKeyHandler(
    IApiKeyRepository keyRepository
) : IRevokeKeyHandler
{
    public async Task<Result<RevokeKeyResponse, Failure>> HandleAsync(
        RevokeKeyCommand command, CancellationToken cancel = default)
    {
        // 1. Guard: key exists (tenantId + keyId)
        var apiKey = await keyRepository.GetByIdAsync(command.KeyId, command.TenantId, cancel);
        if (apiKey is null)
            return FailureProvider.CreateFailure(RevokeKeyFailureCodes.KeyNotFound);

        // 2. Guard: reason required (INV-7)
        if (string.IsNullOrWhiteSpace(command.Reason))
            return FailureProvider.CreateFailure(RevokeKeyFailureCodes.ValidationErrorReasonEmpty);

        // 3. Guard: terminal state (Revoked / Expired can't be revoked again)
        if (apiKey.Status is ApiKeyStatus.Revoked or ApiKeyStatus.Expired)
            return FailureProvider.CreateFailure(RevokeKeyFailureCodes.KeyInTerminalState);

        // 4. Capture previous status, transition, persist
        var previousStatus = apiKey.Status;
        apiKey.Revoke(command.Reason);
        await keyRepository.UpdateAsync(apiKey, cancel);

        return new RevokeKeyResponse(
            KeyId: apiKey.Id,
            PreviousStatus: previousStatus,
            LifecycleStatus: apiKey.Status,
            Reason: command.Reason);
    }
}
