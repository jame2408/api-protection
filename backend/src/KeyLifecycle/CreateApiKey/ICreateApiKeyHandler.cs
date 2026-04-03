using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public interface ICreateApiKeyHandler
{
    Task<Result<CreateApiKeyResponse, Failure>> HandleAsync(
        CreateApiKeyCommand command, CancellationToken cancel = default);
}
