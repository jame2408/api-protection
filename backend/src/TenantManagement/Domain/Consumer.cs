using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.TenantManagement.Domain;

public class Consumer : AggregateRoot<string>
{
    public string TenantId { get; private set; } = string.Empty;

    private Consumer() { }

    public Consumer(string consumerId, string tenantId) : base(consumerId)
    {
        TenantId = tenantId;
    }
}
