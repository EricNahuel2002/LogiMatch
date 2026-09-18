using Domain.ValueObjects;

namespace Domain.Entities;

public class Driver : User
{
    public decimal SalaryPerHour { get; set; }

    public Coordinate? CurrentLocation { get; private set; }

    public TimeSpan? AverageTimeBetweenOrders { get; private set; }

    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Vehicle> Vehicles { get; set; } = [];

    public void SetCurrentLocation(Coordinate location)
    {
        ArgumentNullException.ThrowIfNull(location);
        CurrentLocation = location;
    }

    public void SetAverageTimeBetweenOrders(TimeSpan? value)
    {
        if (value is { } average && average < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The average time between orders cannot be negative.");
        }

        AverageTimeBetweenOrders = value;
    }
}