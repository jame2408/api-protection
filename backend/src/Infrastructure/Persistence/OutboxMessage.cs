namespace ApiKeyManagement.Infrastructure.Persistence;

/// <summary>
/// Transactional outbox row: the authoritative, durable record of a domain event,
/// harvested in the same transaction as the aggregate's own state change
/// (see <see cref="AppDbContext.SaveChangesAsync"/>). ADR-020 §1.
/// Relay to a message broker, <c>ProcessedAt</c> lifecycle, and the
/// version/correlationId/causationId envelope fields are explicitly out of
/// scope until a Relay ADR lands (ADR-020 §1, §3).
/// </summary>
public class OutboxMessage
{
    public Guid EventId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string AggregateId { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
    public string Payload { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
