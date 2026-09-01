using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Shipment : BaseEntity
{
    public ShipmentStatus Status { get; set; }

    public Guid? RouteId { get; set; }
    public Route? Route { get; set; }

    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<ShipmentHistory> History { get; set; } = [];
}