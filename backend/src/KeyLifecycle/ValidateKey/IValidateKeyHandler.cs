using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

public interface IValidateKeyHandler
{
    Task<Result<ValidateKeyResponse, Failure>> HandleAsync(
        ValidateKeyCommand command, CancellationToken cancel = default);
}
