using Domain.Common;

namespace Domain.Entities;

public class DeliveryAttempt : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    public Guid? RouteStopId { get; set; }
    public RouteStop? RouteStop { get; set; }

    public int AttemptNumber { get; set; }
    public DateTime AttemptedAt { get; set; }
    public bool Succeeded { get; set; }
    public string? Note { get; set; }
}