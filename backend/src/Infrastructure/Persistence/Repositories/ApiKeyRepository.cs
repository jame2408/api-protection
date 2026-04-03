using ApiKeyManagement.KeyLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiKeyManagement.Infrastructure.Persistence.Repositories;

public class ApiKeyRepository(AppDbContext db) : IApiKeyRepository
{
    public async Task SaveAsync(ApiKey apiKey, CancellationToken cancel = default)
    {
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancel);
    }

    public async Task<int> CountActiveAsync(
        string consumerId, string environment, string tenantId, CancellationToken cancel = default)
    {
        return await db.ApiKeys.CountAsync(k =>
            k.ConsumerId == consumerId &&
            k.Environment == environment &&
            k.TenantId == tenantId &&
            k.Status == ApiKeyStatus.ACTIVE, cancel);
    }

    public async Task<bool> ExistsNameAsync(
        string name, string consumerId, string environment, string tenantId,
        CancellationToken cancel = default)
    {
        return await db.ApiKeys.AnyAsync(k =>
            k.Name == name &&
            k.ConsumerId == consumerId &&
            k.Environment == environment &&
            k.TenantId == tenantId, cancel);
    }
}
