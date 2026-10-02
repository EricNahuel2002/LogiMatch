using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class DepositConfiguration : IEntityTypeConfiguration<Deposit>
{
    public void Configure(EntityTypeBuilder<Deposit> builder)
    {
        builder.ToTable("Deposits");

        builder.Property(d => d.Address).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Active).IsRequired().HasDefaultValue(true);

        builder.HasIndex(d => d.Active);

        builder.OwnsOne(d => d.Coordinate, o =>
        {
            o.Property(c => c.Latitude).HasColumnType("decimal(9,6)");
            o.Property(c => c.Longitude).HasColumnType("decimal(9,6)");
        });
    }
}
