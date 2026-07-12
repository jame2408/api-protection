using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.UnlockKey;

public interface IUnlockKeyHandler
{
    Task<Result<UnlockKeyResponse, Failure>> HandleAsync(
        UnlockKeyCommand command, CancellationToken cancel = default);
}
