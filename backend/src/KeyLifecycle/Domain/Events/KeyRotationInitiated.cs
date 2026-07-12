using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyRotationInitiated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OldKeyId,
    Guid NewKeyId,
    DateTimeOffset GraceDeadline
) : IDomainEvent;
