namespace Domain.Entities;

public class Customer : User
{
    public int AbsentDeliveriesCount { get; private set; }
    public int SucceededDeliveriesCount { get; private set; }

    public ICollection<Order> Orders { get; set; } = [];

    public void RegisterAbsentDelivery()
    {
        AbsentDeliveriesCount++;
    }

    public void RegisterSuccessfulDelivery()
    {
        SucceededDeliveriesCount++;
    }
}