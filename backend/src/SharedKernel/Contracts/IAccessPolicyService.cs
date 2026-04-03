namespace ApiKeyManagement.SharedKernel.Contracts;

public interface IAccessPolicyService
{
    Task<Guid> CreateDefaultPolicyAsync(Guid keyId, string tenantId, CancellationToken ct = default);
}
