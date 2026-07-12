using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.LockKey;

public interface ILockKeyHandler
{
    Task<Result<LockKeyResponse, Failure>> HandleAsync(
        LockKeyCommand command, CancellationToken cancel = default);
}
