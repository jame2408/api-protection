using ApiKeyManagement.AccessPolicy.Domain;

namespace ApiKeyManagement.Infrastructure.Persistence.Repositories;

public class AccessPolicyRepository(AppDbContext db) : IAccessPolicyRepository
{
    public async Task SaveAsync(
        ApiKeyManagement.AccessPolicy.Domain.AccessPolicy policy,
        CancellationToken ct = default)
    {
        db.AccessPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
    }
}
