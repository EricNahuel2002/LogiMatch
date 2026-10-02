namespace Application.RoutePlanning;

/// <summary>
/// The complete routing problem handed to <see cref="IVehicleRoutingSolver" />.
/// </summary>
/// <remarks>
/// Locations are laid out so that node index and matrix index are interchangeable:
/// <code>
/// [0     .. K)      driver current locations        K = Drivers.Count
/// [K     .. K+S)    shipment destinations          S = Shipments.Count
/// [K+S   .. K+S+D)  deposits, one per distinct DepositId
/// </code>
/// Driver <c>i</c> starts at node <c>i</c> and ends at its deposit node. Shipment
/// <c>j</c> lives at node <c>K + j</c>.
/// </remarks>
public sealed class VehicleRoutingProblem
{
    private readonly Dictionary<Guid, int> _depositNodePositions;

    public VehicleRoutingProblem(
        IReadOnlyList<PlanningShipmentData> shipments,
        IReadOnlyList<PlanningDriverData> drivers,
        IReadOnlyList<PlanningVehicleData> vehicles,
        IReadOnlyList<Guid> depositNodeOrder,
        long horizonSeconds,
        RouteMatrix distanceMatrix,
        RouteMatrix durationMatrix)
    {
        ArgumentNullException.ThrowIfNull(shipments);
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(depositNodeOrder);
        ArgumentNullException.ThrowIfNull(distanceMatrix);
        ArgumentNullException.ThrowIfNull(durationMatrix);

        if (horizonSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizonSeconds),
                "The planning horizon must be greater than zero.");
        }

        Shipments = shipments;
        Drivers = drivers;
        Vehicles = vehicles;
        DepositNodeOrder = depositNodeOrder;
        HorizonSeconds = horizonSeconds;
        DistanceMatrix = distanceMatrix;
        DurationMatrix = durationMatrix;

        _depositNodePositions = depositNodeOrder
            .Select((depositId, position) => new { depositId, position })
            .ToDictionary(x => x.depositId, x => x.position);

        var expected = NodeCount;
        if (distanceMatrix.Size != expected || durationMatrix.Size != expected)
        {
            throw new ArgumentException(
                $"The matrices must be {expected}x{expected} to match the {expected} problem nodes.",
                nameof(distanceMatrix));
        }

        EnsureWindowsFitHorizon();
    }

    /// <summary>
    /// Every delivery window must sit inside the horizon. OR-Tools aborts the whole process
    /// with an opaque native failure when a cumul range escapes the dimension capacity, so the
    /// bound is checked here instead of being pushed onto the native solver.
    /// </summary>
    private void EnsureWindowsFitHorizon()
    {
        foreach (var shipment in Shipments)
        {
            if (shipment.DeliveryWindow is not { } window)
            {
                continue;
            }

            if (window.StartSeconds < 0 || window.EndSeconds > HorizonSeconds)
            {
                throw new ArgumentException(
                    $"Shipment {shipment.ShipmentId} has a delivery window of " +
                    $"[{window.StartSeconds}, {window.EndSeconds}] seconds that falls outside " +
                    $"the planning horizon of {HorizonSeconds} seconds.",
                    nameof(Shipments));
            }
        }
    }

    public IReadOnlyList<PlanningShipmentData> Shipments { get; }

    public IReadOnlyList<PlanningDriverData> Drivers { get; }

    public IReadOnlyList<PlanningVehicleData> Vehicles { get; }

    public IReadOnlyList<Guid> DepositNodeOrder { get; }

    /// <summary>
    /// Longest route the solver may plan, in seconds, measured from the horizon start.
    /// </summary>
    public long HorizonSeconds { get; }

    public RouteMatrix DistanceMatrix { get; }

    public RouteMatrix DurationMatrix { get; }

    public int DriverCount => Drivers.Count;

    public int ShipmentCount => Shipments.Count;

    public int NodeCount => DriverCount + ShipmentCount + DepositNodeOrder.Count;

    public int ShipmentNodeOffset => DriverCount;

    public int DepositNodeOffset => DriverCount + ShipmentCount;

    /// <summary>
    /// Matrix index of the deposit node for the driver at <paramref name="driverIndex" />.
    /// </summary>
    public int GetDepositNodeIndex(int driverIndex)
    {
        var depositId = Drivers[driverIndex].DepositId;
        return _depositNodePositions.TryGetValue(depositId, out var position)
            ? DepositNodeOffset + position
            : throw new InvalidOperationException(
                $"The deposit {depositId} is missing from the problem deposit order.");
    }

    /// <summary>
    /// Index of the vehicle the driver at <paramref name="driverIndex" /> will use.
    /// </summary>
    public int GetVehicleIndex(int driverIndex)
    {
        var vehicleId = Drivers[driverIndex].VehicleId;
        for (var i = 0; i < Vehicles.Count; i++)
        {
            if (Vehicles[i].VehicleId == vehicleId)
            {
                return i;
            }
        }

        throw new InvalidOperationException(
            $"The vehicle {vehicleId} selected for a driver is missing from the problem vehicles.");
    }
}
