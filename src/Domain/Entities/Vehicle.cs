using Domain.Common;

namespace Domain.Entities;

public class Vehicle : BaseEntity
{
    public string LicensePlate { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public decimal CapacityKg { get; set; }

    public ICollection<Driver> Drivers { get; set; } = [];
    public ICollection<Shipment> Shipments { get; set; } = [];
}