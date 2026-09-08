using Domain.Common;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Route : BaseEntity
{
    private Route()
    {
    }

    public Coordinate Origin { get; private set; } = new(0, 0);

    public Guid? DriverId { get; private set; }
    public Driver? Driver { get; private set; }

    public Guid? VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public ICollection<RouteStop> RouteStops { get; private set; } = [];

    public static Route Create(Coordinate origin) => new() { Origin = origin };

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
}