using ApiKeyManagement.KeyLifecycle.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiKeyManagement.Infrastructure.Persistence.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.ConsumerId).IsRequired().HasMaxLength(200);
        builder.Property(k => k.TenantId).IsRequired().HasMaxLength(200);
        builder.Property(k => k.Name).IsRequired().HasMaxLength(200);
        builder.Property(k => k.Environment).IsRequired().HasMaxLength(50);
        builder.Property(k => k.Status).IsRequired().HasConversion<string>();
        builder.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(100);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(200);
        builder.Property(k => k.PolicyId).IsRequired();
        builder.Property(k => k.CreatedAt).IsRequired();
        builder.Property(k => k.ExpiresAt).IsRequired();
        builder.Property(k => k.SuccessorKeyId);
        builder.Property(k => k.PredecessorKeyId);

        // Scopes stored as JSON array
        builder.Property(k => k.Scopes)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Ignore(k => k.DomainEvents);

        builder.HasIndex(k => new { k.ConsumerId, k.Environment, k.TenantId, k.Status });
        builder.HasIndex(k => new { k.Name, k.ConsumerId, k.Environment, k.TenantId });
    }
}
