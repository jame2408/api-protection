namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public interface ICreateApiKeyHandler
{
    Task<CreateApiKeyResponse> HandleAsync(CreateApiKeyCommand command, CancellationToken ct = default);
}
