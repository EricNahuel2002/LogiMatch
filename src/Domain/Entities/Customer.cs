namespace Domain.Entities;

public class Customer : User
{
    public ICollection<Order> Orders { get; set; } = [];
}