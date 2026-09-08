using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class ShipmentHistory : BaseEntity
{
    public Guid ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;

    public ShipmentStatus Status { get; set; }

    public Guid? RouteStopId { get; set; }
    public RouteStop? RouteStop { get; set; }

    public string? Note { get; set; }
    public DateTime RecordedAt { get; set; }
}