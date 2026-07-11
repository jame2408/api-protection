using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ResumeKey;

public class ResumeKeyHandler(
    IApiKeyRepository keyRepository
) : IResumeKeyHandler
{
    public async Task<Result<ResumeKeyResponse, Failure>> HandleAsync(
        ResumeKeyCommand command, CancellationToken cancel = default)
    {
        // 1. Guard: key exists (tenantId + keyId). No actor-type guard here — invariant 6
        // ("human-only") only constrains Suspend (api-spec.md §3.2.6 / design-doc T11); Resume
        // has no equivalent restriction and this feature has no System-resume scenario.
        var apiKey = await keyRepository.GetByIdAsync(command.KeyId, command.TenantId, cancel);
        if (apiKey is null)
            return FailureProvider.CreateFailure(ResumeKeyFailureCodes.KeyNotFound);

        // 2. Guard: must be Suspended
        if (apiKey.Status != ApiKeyStatus.Suspended)
            return FailureProvider.CreateFailure(ResumeKeyFailureCodes.InvalidStateTransition);

        // 3. Transition, persist
        apiKey.Resume(command.ResumedBy);
        await keyRepository.UpdateAsync(apiKey, cancel);

        return new ResumeKeyResponse(
            KeyId: apiKey.Id,
            LifecycleStatus: apiKey.Status,
            ResumedBy: command.ResumedBy.Id);
    }
}
