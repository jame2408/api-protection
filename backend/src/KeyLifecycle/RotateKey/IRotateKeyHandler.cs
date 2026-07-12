using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.RotateKey;

public interface IRotateKeyHandler
{
    Task<Result<RotateKeyResponse, Failure>> HandleAsync(
        RotateKeyCommand command, CancellationToken cancel = default);
}
