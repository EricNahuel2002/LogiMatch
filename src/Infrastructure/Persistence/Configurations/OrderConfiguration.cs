using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.Quantity).IsRequired();

        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.CreatedBy)
            .WithMany(a => a.CreatedOrders)
            .HasForeignKey(o => o.CreatedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.AssignedDriver)
            .WithMany(d => d.Orders)
            .HasForeignKey(o => o.AssignedDriverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.Shipment)
            .WithMany(s => s.Orders)
            .HasForeignKey(o => o.ShipmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}