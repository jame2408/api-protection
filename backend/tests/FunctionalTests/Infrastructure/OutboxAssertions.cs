using System.Text.Json;
using ApiKeyManagement.Infrastructure.Persistence;
using FluentAssertions;

namespace ApiKeyManagement.FunctionalTests.Infrastructure;

/// <summary>
/// Shared outbox assertion helper for Then steps that verify a domain event was harvested
/// into the outbox. Seeding via ApiKey.Create() also emits its own event (e.g. KeyCreated)
/// into the same outbox, so a scenario's outbox is never expected to hold exactly one row —
/// always filter by EventType + AggregateId to isolate the event under test
/// (ADR-020 §4 assertion contract).
/// </summary>
public static class OutboxAssertions
{
    public static JsonDocument RequireOutboxEvent(this AppDbContext db, string eventType, Guid aggregateId)
    {
        var outboxRow = db.OutboxMessages.SingleOrDefault(m =>
            m.EventType == eventType && m.AggregateId == aggregateId.ToString());

        outboxRow.Should().NotBeNull(
            $"a {eventType} domain event must be harvested into the outbox (ADR-020)");

        return JsonDocument.Parse(outboxRow!.Payload);
    }
}
