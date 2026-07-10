using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeySuspended(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    Actor SuspendedBy,
    string Reason
) : IDomainEvent;
