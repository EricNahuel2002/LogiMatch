using Domain.Common;
using Domain.ValueObjects;

namespace Domain.Entities;

public class RouteStop : BaseEntity
{
    public Guid RouteId { get; set; }
    public Route Route { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public Coordinate Coordinate { get; set; } = new(0, 0);
    public int StopOrder { get; set; }
}