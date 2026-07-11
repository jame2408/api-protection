using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyResumed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    Actor ResumedBy
) : IDomainEvent;
