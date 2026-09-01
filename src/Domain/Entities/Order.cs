using Domain.Common;

namespace Domain.Entities;

public class Order : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid CreatedByAdminId { get; set; }
    public Admin CreatedBy { get; set; } = null!;

    public Guid? AssignedDriverId { get; set; }
    public Driver? AssignedDriver { get; set; }

    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
}