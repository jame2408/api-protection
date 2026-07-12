using System.Text.Json;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain.Events;

public record KeyLocked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid KeyId,
    string RuleId,
    string Reason,
    JsonElement Evidence
) : IDomainEvent;
