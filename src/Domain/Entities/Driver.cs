using Domain.ValueObjects;

namespace Domain.Entities;

public class Driver : User
{
    public Coordinate? CurrentLocation { get; private set; }

    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Vehicle> Vehicles { get; set; } = [];

    public void SetCurrentLocation(Coordinate location)
    {
        ArgumentNullException.ThrowIfNull(location);
        CurrentLocation = location;
    }
}