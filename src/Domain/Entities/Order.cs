using Domain.Common;

namespace Domain.Entities;

public class Order : BaseEntity
{
    private Order()
    {
    }

    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;

    public Guid CreatedByAdminId { get; private set; }
    public Admin CreatedBy { get; private set; } = null!;

    public Guid? AssignedDriverId { get; private set; }
    public Driver? AssignedDriver { get; private set; }

    public Guid? ShipmentId { get; private set; }
    public Shipment? Shipment { get; private set; }

    public ICollection<OrderItem> Items { get; private set; } = [];
    public ICollection<DeliveryAttempt> DeliveryAttempts { get; private set; } = [];

    public static Order Create(Customer customer, Admin createdBy, IEnumerable<OrderItem> items)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(createdBy);
        ArgumentNullException.ThrowIfNull(items);

        var order = new Order
        {
            Customer = customer,
            CreatedBy = createdBy
        };

        foreach (var item in items)
        {
            order.AddItem(item);
        }

        if (order.Items.Count == 0)
        {
            throw new InvalidOperationException("An order requires at least one item.");
        }

        return order;
    }

    public void AddItem(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureNotDispatched();
        Items.Add(item);
    }

    public void SetAssignedDriver(Guid? driverId)
    {
        AssignedDriverId = driverId;
    }

    internal void AssignToShipment(Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        if (ShipmentId.HasValue)
        {
            throw new InvalidOperationException("The order is already assigned to a shipment.");
        }

        ShipmentId = shipment.Id;
        Shipment = shipment;
    }

    private void EnsureNotDispatched()
    {
        if (ShipmentId.HasValue)
        {
            throw new InvalidOperationException("An order cannot be modified after being assigned to a shipment.");
        }
    }
}