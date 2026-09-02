using Domain.Common;
using Domain.ValueObjects;

namespace Domain.Entities;

public class RouteStop : BaseEntity
{
    private RouteStop()
    {
    }

    public Guid RouteId { get; private set; }
    public Route Route { get; private set; } = null!;

    public Guid ShipmentId { get; private set; }
    public Shipment Shipment { get; private set; } = null!;

    public string? Name { get; private set; }
    public Coordinate Coordinate { get; private set; } = new(0, 0);
    public int StopOrder { get; private set; }

    public static RouteStop Create(
        Shipment shipment,
        Coordinate coordinate,
        int stopOrder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        if (stopOrder <= 0)
        {
            throw new ArgumentException("Stop order must be greater than zero.", nameof(stopOrder));
        }

        return new RouteStop
        {
            ShipmentId = shipment.Id,
            Shipment = shipment,
            Coordinate = coordinate,
            StopOrder = stopOrder,
            Name = name
        };
    }

    public void AssignToRoute(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        RouteId = route.Id;
        Route = route;
    }
}