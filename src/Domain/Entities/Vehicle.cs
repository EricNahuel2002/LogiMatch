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

    public ICollection<Driver> Drivers { get; private set; } = [];
    public ICollection<Shipment> Shipments { get; private set; } = [];

    public static Vehicle Create(string licensePlate, decimal capacityKg, string? brand = null, string? model = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licensePlate);

        if (capacityKg <= 0)
        {
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacityKg));
        }

        return new Vehicle
        {
            LicensePlate = licensePlate,
            CapacityKg = capacityKg,
            Brand = brand,
            Model = model
        };
    }

    public void SetActive(bool active) => Active = active;
}