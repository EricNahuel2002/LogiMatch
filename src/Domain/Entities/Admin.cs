namespace Domain.Entities;

public class Admin : User
{
    public ICollection<Order> CreatedOrders { get; set; } = [];
}