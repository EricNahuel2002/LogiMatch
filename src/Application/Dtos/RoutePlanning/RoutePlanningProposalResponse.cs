namespace Application.Dtos.RoutePlanning;

public sealed record RoutePlanningProposalResponse(
    IReadOnlyList<RouteAssignmentResponse> Assignments,
    int DriverCount,
    int VehicleCount,
    int ShipmentCount,
    long TotalDistanceMeters,
    int TotalDurationMinutes,
    IReadOnlyList<ExcludedShipmentResponse> ExcludedShipments);

public sealed record RouteAssignmentResponse(
    Guid DriverId,
    Guid VehicleId,
    Guid DepositId,
    decimal LoadKg,
    IReadOnlyList<PlannedStopResponse> Stops);

public sealed record PlannedStopResponse(Guid ShipmentId, int StopOrder);

/// <summary>
/// A pending shipment that could not take part in the proposal, with the reason why. Excluded
/// shipments are not dropped silently: the admin needs to know they are still waiting for a
/// route before the proposal can be committed.
/// </summary>
public sealed record ExcludedShipmentResponse(Guid ShipmentId, string Reason);
