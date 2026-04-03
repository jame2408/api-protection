using ApiKeyManagement.AccessPolicy.Domain;
using ApiKeyManagement.SharedKernel.Contracts;
using AccessPolicyEntity = ApiKeyManagement.AccessPolicy.Domain.AccessPolicy;

namespace ApiKeyManagement.AccessPolicy.Application;

public class AccessPolicyService(IAccessPolicyRepository repository) : IAccessPolicyService
{
    public async Task<Guid> CreateDefaultPolicyAsync(
        Guid keyId, string tenantId, CancellationToken ct = default)
    {
        var policy = AccessPolicyEntity.CreateDefault(keyId, tenantId);
        await repository.SaveAsync(policy, ct);
        return policy.Id;
    }
}
