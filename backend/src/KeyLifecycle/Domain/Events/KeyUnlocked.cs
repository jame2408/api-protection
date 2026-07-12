using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyUnlocked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    Actor UnlockedBy
) : IDomainEvent;
