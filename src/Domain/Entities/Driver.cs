namespace Domain.Entities;

public class Driver : User
{
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Shipment> Shipments { get; set; } = [];
    public ICollection<Vehicle> Vehicles { get; set; } = [];
}