using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    string ConsumerId,
    string TenantId,
    string Environment,
    IReadOnlyList<string> Scopes,
    string KeyPrefix,
    DateTimeOffset ExpiresAt,
    Guid PolicyId
) : IDomainEvent;
