using Domain.Common;

namespace Domain.Entities;

public class Vehicle : BaseEntity
{
    private Vehicle()
    {
    }

    public string LicensePlate { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public decimal CapacityKg { get; private set; }
    public bool Active { get; private set; } = true;
    public decimal KilometersPerDay { get; private set; }
    public decimal FuelConsumption { get; private set; }
    public decimal FuelPrice { get; private set; }
    public decimal MaintenanceCost { get; private set; }

    public ICollection<Driver> Drivers { get; private set; } = [];
    public ICollection<Route> Routes { get; private set; } = [];

    public static Vehicle Create(
        string licensePlate,
        decimal capacityKg,
        string? brand = null,
        string? model = null,
        decimal fuelConsumption = 0,
        decimal fuelPrice = 0,
        decimal maintenanceCost = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licensePlate);

        if (capacityKg <= 0)
        {
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacityKg));
        }

        if (fuelConsumption < 0)
        {
            throw new ArgumentException(
                "Fuel consumption must be greater than or equal to zero.",
                nameof(fuelConsumption));
        }

        if (fuelPrice < 0)
        {
            throw new ArgumentException("Fuel price must be greater than or equal to zero.", nameof(fuelPrice));
        }

        if (maintenanceCost < 0)
        {
            throw new ArgumentException(
                "Maintenance cost must be greater than or equal to zero.",
                nameof(maintenanceCost));
        }

        return new Vehicle
        {
            LicensePlate = licensePlate,
            CapacityKg = capacityKg,
            Brand = brand,
            Model = model,
            FuelConsumption = fuelConsumption,
            FuelPrice = fuelPrice,
            MaintenanceCost = maintenanceCost
        };
    }

    public void SetActive(bool active) => Active = active;

    public void SetOperatingCosts(decimal fuelConsumption, decimal fuelPrice, decimal maintenanceCost)
    {
        if (fuelConsumption < 0)
        {
            throw new ArgumentException(
                "Fuel consumption must be greater than or equal to zero.",
                nameof(fuelConsumption));
        }

        if (fuelPrice < 0)
        {
            throw new ArgumentException("Fuel price must be greater than or equal to zero.", nameof(fuelPrice));
        }

        if (maintenanceCost < 0)
        {
            throw new ArgumentException(
                "Maintenance cost must be greater than or equal to zero.",
                nameof(maintenanceCost));
        }

        FuelConsumption = fuelConsumption;
        FuelPrice = fuelPrice;
        MaintenanceCost = maintenanceCost;
    }

    public decimal RecalculateKilometersPerDay()
    {
        var totalMeters = Routes
            .SelectMany(r => r.RouteStops)
            .Where(rs => rs.Shipment.ArrivedAtDestination)
            .Sum(rs => rs.DistanceMeters);

        KilometersPerDay = totalMeters / 1000m;
        return KilometersPerDay;
    }
}