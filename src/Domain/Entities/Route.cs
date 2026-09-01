using Domain.Common;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Route : BaseEntity
{
    public Coordinate Origin { get; set; } = new(0, 0);
    public Coordinate Destination { get; set; } = new(0, 0);

    public ICollection<RouteStop> RouteStops { get; set; } = [];
}