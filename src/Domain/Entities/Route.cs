using Domain.Common;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Route : BaseEntity
{
    public Coordinate Origin { get; set; }
    public Coordinate Destination { get; set; }

    public ICollection<RouteStop> RouteStops { get; set; } = [];
}