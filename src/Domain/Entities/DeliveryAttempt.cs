using Domain.Common;

namespace Domain.Entities;

public class DeliveryAttempt : BaseEntity
{
    private DeliveryAttempt()
    {
    }

    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;

    public Guid ShipmentId { get; private set; }
    public Shipment Shipment { get; private set; } = null!;

    public Guid? RouteStopId { get; private set; }
    public RouteStop? RouteStop { get; private set; }

    public int AttemptNumber { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public bool Succeeded { get; private set; }
    public string? Note { get; private set; }

    public static DeliveryAttempt Create(
        Order order,
        Shipment shipment,
        RouteStop? routeStop,
        int attemptNumber,
        DateTime attemptedAt,
        bool succeeded,
        string? note)
    {
        return new DeliveryAttempt
        {
            Order = order,
            Shipment = shipment,
            RouteStop = routeStop,
            AttemptNumber = attemptNumber,
            AttemptedAt = attemptedAt,
            Succeeded = succeeded,
            Note = note
        };
    }
}