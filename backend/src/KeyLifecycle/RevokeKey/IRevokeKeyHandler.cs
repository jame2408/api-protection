using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeKey;

public interface IRevokeKeyHandler
{
    Task<Result<RevokeKeyResponse, Failure>> HandleAsync(
        RevokeKeyCommand command, CancellationToken cancel = default);
}
