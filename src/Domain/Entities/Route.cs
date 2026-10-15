using Domain.Common;
using Domain.Enums;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Route : BaseEntity
{
    private Route()
    {
    }

    public Coordinate Origin { get; private set; } = new(0, 0);

    public bool IsActive { get; private set; } = true;

    public RouteCancellationReason? CancellationReason { get; private set; }

    public Guid? DriverId { get; private set; }
    public Driver? Driver { get; private set; }

    public Guid? VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public ICollection<RouteStop> RouteStops { get; private set; } = [];

    public static Route Create(Coordinate origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        // Origin is mapped as an owned type, and an owned instance cannot belong to two routes.
        // Copying keeps two routes created from the same Coordinate (for example the same deposit)
        // independent instead of leaving one of them with NULL origin columns.
        return new() { Origin = new Coordinate(origin.Latitude, origin.Longitude) };
    }

    public void AssignDriver(Driver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        DriverId = driver.Id;
        Driver = driver;
    }

    public void AssignVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        VehicleId = vehicle.Id;
        Vehicle = vehicle;
    }

    public void AddStop(RouteStop routeStop)
    {
        ArgumentNullException.ThrowIfNull(routeStop);

        if (routeStop.StopOrder <= 0)
        {
            throw new ArgumentException("Stop order must be greater than zero.", nameof(routeStop));
        }

        if (RouteStops.Any(rs => rs.StopOrder == routeStop.StopOrder))
        {
            throw new InvalidOperationException(
                $"A route cannot have two stops with the same order ({routeStop.StopOrder}).");
        }

        RouteStops.Add(routeStop);
    }

    public void Deactivate(RouteCancellationReason reason)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("The route is already inactive.");
        }

        IsActive = false;
        CancellationReason = reason;
    }
}