namespace ApiKeyManagement.KeyLifecycle.Domain;

public interface IApiKeyRepository
{
    Task SaveAsync(ApiKey apiKey, CancellationToken ct = default);
    Task<int> CountActiveAsync(string consumerId, string environment, string tenantId, CancellationToken ct = default);
    Task<bool> ExistsNameAsync(string name, string consumerId, string environment, string tenantId, CancellationToken ct = default);
}
