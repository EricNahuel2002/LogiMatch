using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ShipmentHistoryConfiguration : IEntityTypeConfiguration<ShipmentHistory>
{
    public void Configure(EntityTypeBuilder<ShipmentHistory> builder)
    {
        builder.ToTable("ShipmentHistory");

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(h => h.Note).HasMaxLength(500);
        builder.Property(h => h.RecordedAt).IsRequired();

        builder.HasOne(h => h.RouteStop)
            .WithMany()
            .HasForeignKey(h => h.RouteStopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}