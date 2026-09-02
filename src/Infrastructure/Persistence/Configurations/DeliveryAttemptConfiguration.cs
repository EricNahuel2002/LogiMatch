using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("DeliveryAttempts");

        builder.Property(a => a.AttemptNumber).IsRequired();
        builder.Property(a => a.AttemptedAt).IsRequired();
        builder.Property(a => a.Succeeded).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(500);

        builder.HasIndex(a => new { a.OrderId, a.AttemptNumber }).IsUnique();

        builder.HasOne(a => a.Order)
            .WithMany(o => o.DeliveryAttempts)
            .HasForeignKey(a => a.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Shipment)
            .WithMany(s => s.DeliveryAttempts)
            .HasForeignKey(a => a.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.RouteStop)
            .WithMany()
            .HasForeignKey(a => a.RouteStopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}