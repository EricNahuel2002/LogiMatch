using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.ToTable("RouteStops");

        builder.Property(rs => rs.Name).HasMaxLength(200).IsRequired();
        builder.Property(rs => rs.StopOrder).IsRequired();

        builder.OwnsOne(rs => rs.Coordinate, o =>
        {
            o.Property(c => c.Latitude).HasColumnType("decimal(9,6)");
            o.Property(c => c.Longitude).HasColumnType("decimal(9,6)");
        });
    }
}