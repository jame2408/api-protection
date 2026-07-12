using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyGracePeriodExpired(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    Guid SuccessorKeyId
) : IDomainEvent;
