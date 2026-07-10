using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyLeakNotificationRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    IReadOnlyList<string> Audiences
) : IDomainEvent;
