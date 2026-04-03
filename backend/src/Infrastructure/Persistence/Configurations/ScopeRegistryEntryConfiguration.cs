using ApiKeyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiKeyManagement.Infrastructure.Persistence.Configurations;

public class ScopeRegistryEntryConfiguration : IEntityTypeConfiguration<ScopeRegistryEntry>
{
    public void Configure(EntityTypeBuilder<ScopeRegistryEntry> builder)
    {
        builder.ToTable("ScopeRegistry");
        builder.HasKey(s => s.ScopeName);
        builder.Property(s => s.ScopeName).HasMaxLength(200);
    }
}
