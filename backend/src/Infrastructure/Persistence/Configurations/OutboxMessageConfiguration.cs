using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiKeyManagement.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(o => o.EventId);
        builder.Property(o => o.EventType).IsRequired().HasMaxLength(200);
        builder.Property(o => o.AggregateId).IsRequired().HasMaxLength(200);
        builder.Property(o => o.OccurredAt).IsRequired();
        builder.Property(o => o.Payload)
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
    }
}
