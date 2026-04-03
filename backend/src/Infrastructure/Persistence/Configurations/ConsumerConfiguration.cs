using ApiKeyManagement.TenantManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiKeyManagement.Infrastructure.Persistence.Configurations;

public class ConsumerConfiguration : IEntityTypeConfiguration<Consumer>
{
    public void Configure(EntityTypeBuilder<Consumer> builder)
    {
        builder.ToTable("Consumers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(200);
        builder.Property(c => c.TenantId).IsRequired().HasMaxLength(200);
        builder.HasIndex(c => c.TenantId);
        builder.Ignore(c => c.DomainEvents);
    }
}
