using ApiKeyManagement.KeyLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiKeyManagement.Infrastructure.Persistence.Repositories;

public class ScopeRegistryService(AppDbContext db) : IScopeRegistry
{
    public async Task<bool> AllExistAsync(
        IEnumerable<string> scopes, CancellationToken ct = default)
    {
        var scopeList = scopes.ToList();
        if (scopeList.Count == 0) return false;

        var existingCount = await db.ScopeRegistryEntries
            .CountAsync(s => scopeList.Contains(s.ScopeName), ct);

        return existingCount == scopeList.Count;
    }
}
