using ApiKeyManagement.AccessPolicy.Domain;

namespace ApiKeyManagement.Infrastructure.Persistence.Repositories;

public class AccessPolicyRepository(AppDbContext db) : IAccessPolicyRepository
{
    public async Task SaveAsync(
        ApiKeyManagement.AccessPolicy.Domain.AccessPolicy policy,
        CancellationToken cancel = default)
    {
        db.AccessPolicies.Add(policy);
        await db.SaveChangesAsync(cancel);
    }
}
