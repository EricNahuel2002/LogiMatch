using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Shipment : BaseEntity
{
    public const int MaxDeliveryAttempts = 3;
    public static readonly TimeSpan MinTimeBetweenAttempts = TimeSpan.FromMinutes(5);

    private Shipment()
    {
    }

    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Pending;

    public Guid? RouteId { get; private set; }
    public Route? Route { get; private set; }

    public Guid? DriverId { get; private set; }
    public Driver? Driver { get; private set; }

    public Guid? VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    public bool ArrivedAtDestination { get; private set; }

    public ICollection<Order> Orders { get; private set; } = [];
    public ICollection<DeliveryAttempt> DeliveryAttempts { get; private set; } = [];
    public ICollection<ShipmentHistory> History { get; private set; } = [];

    public static Shipment Create() => new();

    public void AssignRoute(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);
        EnsurePending();
        RouteId = route.Id;
        Route = route;
    }

    public void AssignDriver(Driver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        EnsurePending();
        DriverId = driver.Id;
        Driver = driver;
    }

    public void AssignVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        EnsurePending();
        VehicleId = vehicle.Id;
        Vehicle = vehicle;
    }

    public void AddOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        EnsurePending();
        order.AssignToShipment(this);
        Orders.Add(order);
    }

    public void Start()
    {
        if (Status != ShipmentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"A shipment can only be started while pending. Current status: {Status}.");
        }

        if (Route is null || Driver is null || Vehicle is null || !Vehicle.Active || Orders.Count == 0)
        {
            throw new InvalidOperationException(
                "A shipment requires a route, an active vehicle, a driver and at least one order to start.");
        }

        Status = ShipmentStatus.InProgress;
        AddHistory(ShipmentStatus.InProgress, null, null);
    }

    public void Stop(RouteStop? routeStop = null, string? note = null)
    {
        EnsureInProgress();
        Status = ShipmentStatus.Stopped;
        AddHistory(ShipmentStatus.Stopped, routeStop, note);
    }

    public void Resume(RouteStop? routeStop = null, string? note = null)
    {
        if (Status != ShipmentStatus.Stopped)
        {
            throw new InvalidOperationException("Only a stopped shipment can resume.");
        }

        Status = ShipmentStatus.InProgress;
        AddHistory(ShipmentStatus.InProgress, routeStop, note);
    }

    public void MarkArrivedAtDestination(string? note = null)
    {
        if (Status != ShipmentStatus.InProgress)
        {
            throw new InvalidOperationException("A shipment must be in progress to arrive at destination.");
        }

        Status = ShipmentStatus.Arrived;
        ArrivedAtDestination = true;
        AddHistory(ShipmentStatus.Arrived, null, note ?? "Arrived at destination");
    }

    public void Cancel(string? note = null)
    {
        if (ArrivedAtDestination)
        {
            throw new InvalidOperationException("A shipment that arrived at destination cannot be cancelled.");
        }

        if (Status == ShipmentStatus.Cancelled)
        {
            throw new InvalidOperationException("The shipment is already cancelled.");
        }

        if (Status == ShipmentStatus.Finalized || Status == ShipmentStatus.DeliveryFailed)
        {
            throw new InvalidOperationException("A completed shipment cannot be cancelled.");
        }

        Status = ShipmentStatus.Cancelled;
        AddHistory(ShipmentStatus.Cancelled, null, note);
    }

    public void RegisterDeliveryAttempt(
        Order order,
        RouteStop? routeStop,
        bool succeeded,
        string? note = null,
        DateTime? attemptedAt = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.ShipmentId != Id)
        {
            throw new InvalidOperationException("The order does not belong to this shipment.");
        }

        if (Status != ShipmentStatus.Arrived)
        {
            throw new InvalidOperationException(
                "A delivery attempt can only be registered once the shipment has arrived.");
        }

        if (DeliveryAttempts.Count >= MaxDeliveryAttempts)
        {
            throw new InvalidOperationException($"A shipment can have at most {MaxDeliveryAttempts} delivery attempts.");
        }

        var timestamp = attemptedAt ?? DateTime.UtcNow;
        var lastAttempt = DeliveryAttempts.MaxBy(a => a.AttemptedAt);
        if (lastAttempt is not null && timestamp - lastAttempt.AttemptedAt < MinTimeBetweenAttempts)
        {
            throw new InvalidOperationException(
                $"Delivery attempts must be at least {MinTimeBetweenAttempts.TotalMinutes} minutes apart.");
        }

        var attemptNumber = DeliveryAttempts.Count == 0
            ? 1
            : DeliveryAttempts.Max(a => a.AttemptNumber) + 1;

        var attempt = DeliveryAttempt.Create(order, this, routeStop, attemptNumber, timestamp, succeeded, note);
        DeliveryAttempts.Add(attempt);
        order.DeliveryAttempts.Add(attempt);

        AddHistory(
            ShipmentStatus.Arrived,
            routeStop,
            $"Delivery attempt {attemptNumber} {(succeeded ? "succeeded" : "failed")}");

        if (succeeded)
        {
            MarkFinalized();
        }
        else if (DeliveryAttempts.Count >= MaxDeliveryAttempts)
        {
            MarkDeliveryFailed();
        }
    }

    private void MarkFinalized()
    {
        Status = ShipmentStatus.Finalized;
        AddHistory(ShipmentStatus.Finalized, null, null);
    }

    private void MarkDeliveryFailed()
    {
        Status = ShipmentStatus.DeliveryFailed;
        AddHistory(ShipmentStatus.DeliveryFailed, null, "Maximum delivery attempts reached");
    }

    private void AddHistory(ShipmentStatus status, RouteStop? routeStop, string? note)
    {
        History.Add(new ShipmentHistory
        {
            Shipment = this,
            Status = status,
            RouteStop = routeStop,
            Note = note,
            RecordedAt = DateTime.UtcNow
        });
    }

    private void EnsurePending()
    {
        if (Status != ShipmentStatus.Pending)
        {
            throw new InvalidOperationException("Shipment assignments can only be changed while the shipment is pending.");
        }
    }

    private void EnsureInProgress()
    {
        if (Status != ShipmentStatus.InProgress)
        {
            throw new InvalidOperationException("Only an in-progress shipment can be stopped.");
        }
    }
}