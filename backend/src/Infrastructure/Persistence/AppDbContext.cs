using System.Text.Json;
using ApiKeyManagement.AccessPolicy.Domain;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;
using ApiKeyManagement.TenantManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiKeyManagement.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), ITenantQueryContext
{
    private static readonly JsonSerializerOptions OutboxPayloadOptions = new(JsonSerializerDefaults.Web);

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AccessPolicy.Domain.AccessPolicy> AccessPolicies => Set<AccessPolicy.Domain.AccessPolicy>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Consumer> Consumers => Set<Consumer>();
    public DbSet<ScopeRegistryEntry> ScopeRegistryEntries => Set<ScopeRegistryEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // ITenantQueryContext implementation
    IQueryable<Tenant> ITenantQueryContext.Tenants => Tenants;
    IQueryable<Consumer> ITenantQueryContext.Consumers => Consumers;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// ADR-020 §2: harvest domain events from tracked aggregates into the outbox
    /// in the same transaction as their own state change, then clear them so they
    /// are never re-harvested on a later SaveChanges call within the same scope.
    /// </summary>
    // CA1725 wants this override's parameter renamed to match DbContext's own
    // `cancellationToken` declaration; repo convention (naming.guide.md) requires
    // `cancel` on every CancellationToken parameter, so the analyzer is suppressed
    // for this single override instead of breaking that convention.
#pragma warning disable CA1725
    public override async Task<int> SaveChangesAsync(CancellationToken cancel = default)
    {
        HarvestDomainEventsIntoOutbox();
        return await base.SaveChangesAsync(cancel);
    }
#pragma warning restore CA1725

    private void HarvestDomainEventsIntoOutbox()
    {
        var entries = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entry in entries)
        {
            var aggregateId = entry.Property("Id").CurrentValue?.ToString() ?? string.Empty;

            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage
                {
                    EventId = domainEvent.EventId,
                    EventType = domainEvent.GetType().Name,
                    AggregateId = aggregateId,
                    OccurredAt = domainEvent.OccurredAt,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), OutboxPayloadOptions),
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }

            entry.Entity.ClearDomainEvents();
        }
    }
}

/// <summary>Reference data entry for the Scope Registry.</summary>
public record ScopeRegistryEntry(string ScopeName);
