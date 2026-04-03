namespace ApiKeyManagement.KeyLifecycle.Domain;

public interface IApiKeyRepository
{
    Task SaveAsync(ApiKey apiKey, CancellationToken cancel = default);
    Task<int> CountActiveAsync(string consumerId, string environment, string tenantId, CancellationToken cancel = default);
    Task<bool> ExistsNameAsync(string name, string consumerId, string environment, string tenantId, CancellationToken cancel = default);
}
