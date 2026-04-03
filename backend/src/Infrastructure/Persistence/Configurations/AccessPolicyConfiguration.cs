using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiKeyManagement.Infrastructure.Persistence.Configurations;

public class AccessPolicyConfiguration
    : IEntityTypeConfiguration<ApiKeyManagement.AccessPolicy.Domain.AccessPolicy>
{
    public void Configure(
        EntityTypeBuilder<ApiKeyManagement.AccessPolicy.Domain.AccessPolicy> builder)
    {
        builder.ToTable("AccessPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.KeyId).IsRequired();
        builder.Property(p => p.TenantId).IsRequired().HasMaxLength(200);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.HasIndex(p => p.KeyId).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}
