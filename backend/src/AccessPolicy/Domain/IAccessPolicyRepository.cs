namespace ApiKeyManagement.AccessPolicy.Domain;

public interface IAccessPolicyRepository
{
    Task SaveAsync(AccessPolicy policy, CancellationToken cancel = default);
}
