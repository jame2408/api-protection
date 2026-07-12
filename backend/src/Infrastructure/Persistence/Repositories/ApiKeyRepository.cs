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
            k.Status == ApiKeyStatus.Active, cancel);
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

    public async Task<bool> ExistsRotatingAsync(
        string consumerId, string environment, string tenantId, CancellationToken cancel = default)
    {
        return await db.ApiKeys.AnyAsync(k =>
            k.ConsumerId == consumerId &&
            k.Environment == environment &&
            k.TenantId == tenantId &&
            k.Status == ApiKeyStatus.Rotating, cancel);
    }

    // Tracked (no AsNoTracking) — caller mutates and persists via UpdateAsync.
    public async Task<ApiKey?> GetByIdAsync(Guid keyId, string tenantId, CancellationToken cancel = default)
    {
        return await db.ApiKeys.FirstOrDefaultAsync(k =>
            k.Id == keyId &&
            k.TenantId == tenantId, cancel);
    }

    public Task UpdateAsync(ApiKey apiKey, CancellationToken cancel = default)
        => db.SaveChangesAsync(cancel);

    // Tracked (no AsNoTracking) — caller (RevokeLeakedKeysHandler) mutates and persists each
    // match via UpdateAsync, same reasoning as GetByIdAsync above.
    public async Task<IReadOnlyList<ApiKey>> GetNonTerminalByPrefixAsync(
        string keyPrefix, CancellationToken cancel = default)
    {
        return await db.ApiKeys.Where(k =>
            k.KeyPrefix == keyPrefix &&
            k.Status != ApiKeyStatus.Revoked &&
            k.Status != ApiKeyStatus.Expired).ToListAsync(cancel);
    }
}
