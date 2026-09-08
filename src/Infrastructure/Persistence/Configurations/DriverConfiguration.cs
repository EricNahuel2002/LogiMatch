using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.OwnsOne(d => d.CurrentLocation, o =>
        {
            o.Property(c => c.Latitude).HasColumnType("decimal(9,6)");
            o.Property(c => c.Longitude).HasColumnType("decimal(9,6)");
        });

        builder.HasMany(d => d.Vehicles)
            .WithMany(v => v.Drivers);
    }
}