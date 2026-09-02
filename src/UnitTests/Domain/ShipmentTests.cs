using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class ShipmentTests
{
    private readonly Vehicle _vehicle = Vehicle.Create("ABC123", 1000m);
    private readonly Driver _driver = new();
    private readonly Route _route = new Route
    {
        Origin = new Coordinate(10, 10),
        Destination = new Coordinate(20, 20)
    };
    private readonly Order _order = Order.Create(new Customer(), new Admin(), [OrderItem.Create("Item", 10m, 1)]);

    private Shipment BuildPendingShipment()
    {
        var shipment = Shipment.Create();
        shipment.AssignRoute(_route);
        shipment.AssignDriver(_driver);
        shipment.AssignVehicle(_vehicle);
        shipment.AddOrder(_order);
        return shipment;
    }

    [Fact]
    public void Start_WithoutRoute_Throws()
    {
        var shipment = Shipment.Create();
        shipment.AssignDriver(_driver);
        shipment.AssignVehicle(_vehicle);
        shipment.AddOrder(_order);

        Assert.Throws<InvalidOperationException>(() => shipment.Start());
    }

    [Fact]
    public void Start_WithoutDriver_Throws()
    {
        var shipment = Shipment.Create();
        shipment.AssignRoute(_route);
        shipment.AssignVehicle(_vehicle);
        shipment.AddOrder(_order);

        Assert.Throws<InvalidOperationException>(() => shipment.Start());
    }

    [Fact]
    public void Start_WithoutVehicle_Throws()
    {
        var shipment = Shipment.Create();
        shipment.AssignRoute(_route);
        shipment.AssignDriver(_driver);
        shipment.AddOrder(_order);

        Assert.Throws<InvalidOperationException>(() => shipment.Start());
    }

    [Fact]
    public void Start_WithInactiveVehicle_Throws()
    {
        _vehicle.SetActive(false);
        var shipment = BuildPendingShipment();

        try
        {
            Assert.Throws<InvalidOperationException>(() => shipment.Start());
        }
        finally
        {
            _vehicle.SetActive(true);
        }
    }

    [Fact]
    public void Start_WithoutOrders_Throws()
    {
        var shipment = Shipment.Create();
        shipment.AssignRoute(_route);
        shipment.AssignDriver(_driver);
        shipment.AssignVehicle(_vehicle);

        Assert.Throws<InvalidOperationException>(() => shipment.Start());
    }

    [Fact]
    public void Start_WhenFullyConfigured_SetsInProgress()
    {
        var shipment = BuildPendingShipment();

        shipment.Start();

        Assert.Equal(ShipmentStatus.InProgress, shipment.Status);
        Assert.Single(shipment.History);
    }

    [Fact]
    public void Stop_WhenInProgress_SetsStopped()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();

        shipment.Stop();

        Assert.Equal(ShipmentStatus.Stopped, shipment.Status);
    }

    [Fact]
    public void Stop_WhenNotInProgress_Throws()
    {
        var shipment = BuildPendingShipment();

        Assert.Throws<InvalidOperationException>(() => shipment.Stop());
    }

    [Fact]
    public void Resume_WhenStopped_SetsInProgress()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        shipment.Stop();

        shipment.Resume();

        Assert.Equal(ShipmentStatus.InProgress, shipment.Status);
    }

    [Fact]
    public void Resume_WhenNotStopped_Throws()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();

        Assert.Throws<InvalidOperationException>(() => shipment.Resume());
    }

    [Fact]
    public void MarkArrivedAtDestination_WhenInProgress_SetsArrived()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();

        shipment.MarkArrivedAtDestination();

        Assert.Equal(ShipmentStatus.Arrived, shipment.Status);
        Assert.True(shipment.ArrivedAtDestination);
    }

    [Fact]
    public void MarkArrivedAtDestination_WhenNotInProgress_Throws()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        shipment.Stop();

        Assert.Throws<InvalidOperationException>(() => shipment.MarkArrivedAtDestination());
    }

    [Fact]
    public void Start_WhenCancelled_Throws()
    {
        var shipment = BuildPendingShipment();
        shipment.Cancel();

        Assert.Throws<InvalidOperationException>(() => shipment.Start());
    }

    [Fact]
    public void Cancel_WhenArrivedAtDestination_Throws()
    {
        var shipment = BuildPendingShipment();
        shipment.Start();
        shipment.MarkArrivedAtDestination();

        Assert.Throws<InvalidOperationException>(() => shipment.Cancel());
    }

    [Fact]
    public void Cancel_WhenPending_SetsCancelled()
    {
        var shipment = BuildPendingShipment();

        shipment.Cancel();

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
    }
}