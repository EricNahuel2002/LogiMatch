namespace Application.RoutePlanning;

/// <summary>
/// Distinguishes a shipment the solver placed from one it could not place, so the caller
/// can report a precise failure instead of just noticing a shorter list.
/// </summary>
public enum PlanningStopStatus
{
    Assigned,
    Infeasible
}

public sealed record VehicleRouteStop(
    Guid ShipmentId,
    int StopOrder,
    PlanningStopStatus Status);

public sealed record VehicleRoutePlan(
    Guid DriverId,
    Guid VehicleId,
    Guid DepositId,
    decimal LoadKg,
    IReadOnlyList<VehicleRouteStop> Stops);

/// <summary>
/// The solver outcome. Totals come from the raw distance and duration matrices along the
/// planned arcs, so they are not distorted by the priority weighting used as the arc cost.
/// </summary>
public sealed record VehicleRoutingSolution(
    IReadOnlyList<VehicleRoutePlan> Routes,
    IReadOnlyList<VehicleRouteStop> Unassigned,
    long TotalDistanceMeters,
    long TotalDurationSeconds)
{
    public bool IsComplete => Unassigned.Count == 0;

    public int TotalDurationMinutes => (int)Math.Ceiling(TotalDurationSeconds / 60.0);
}
