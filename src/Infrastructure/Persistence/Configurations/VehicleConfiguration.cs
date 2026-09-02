using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.Property(v => v.LicensePlate).HasMaxLength(20).IsRequired();
        builder.Property(v => v.Brand).HasMaxLength(100);
        builder.Property(v => v.Model).HasMaxLength(100);
        builder.Property(v => v.CapacityKg).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(v => v.Active).IsRequired().HasDefaultValue(true);

        builder.HasIndex(v => v.LicensePlate).IsUnique();
    }
}