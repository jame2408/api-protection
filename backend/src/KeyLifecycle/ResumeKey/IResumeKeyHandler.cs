using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ResumeKey;

public interface IResumeKeyHandler
{
    Task<Result<ResumeKeyResponse, Failure>> HandleAsync(
        ResumeKeyCommand command, CancellationToken cancel = default);
}
