using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("Routes");

        builder.OwnsOne(r => r.Origin, o =>
        {
            o.Property(c => c.Latitude).HasColumnType("decimal(9,6)");
            o.Property(c => c.Longitude).HasColumnType("decimal(9,6)");
        });

        builder.OwnsOne(r => r.Destination, o =>
        {
            o.Property(c => c.Latitude).HasColumnType("decimal(9,6)");
            o.Property(c => c.Longitude).HasColumnType("decimal(9,6)");
        });

        builder.HasMany(r => r.RouteStops)
            .WithOne(rs => rs.Route)
            .HasForeignKey(rs => rs.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}