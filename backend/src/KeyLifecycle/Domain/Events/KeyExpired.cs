using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyExpired(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    string PreviousStatus
) : IDomainEvent;
