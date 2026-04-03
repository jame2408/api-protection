using ApiKeyManagement.TenantManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiKeyManagement.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasMaxLength(200);
        builder.Property(t => t.Status).IsRequired().HasConversion<string>();
        builder.Ignore(t => t.DomainEvents);
    }
}
