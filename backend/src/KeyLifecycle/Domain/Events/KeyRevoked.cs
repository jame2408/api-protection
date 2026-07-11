using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyRevoked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    string PreviousStatus,
    string Reason,
    Actor RevokedBy
) : IDomainEvent;
