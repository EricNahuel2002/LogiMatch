using Domain.Common;

namespace Domain.Entities;

public class Order : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid CreatedByAdminId { get; set; }
    public Admin CreatedBy { get; set; } = null!;

    public Guid? AssignedDriverId { get; set; }
    public Driver? AssignedDriver { get; set; }

    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<DeliveryAttempt> DeliveryAttempts { get; set; } = [];
}