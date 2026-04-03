using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.AccessPolicy.Domain;

public class AccessPolicy : AggregateRoot<Guid>
{
    public Guid KeyId { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core
    private AccessPolicy() { }

    public static AccessPolicy CreateDefault(Guid keyId, string tenantId)
    {
        return new AccessPolicy
        {
            Id = Guid.NewGuid(),
            KeyId = keyId,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
