using Domain.Enums;

namespace Application.Dtos.RoutePlanning;

public sealed record RoutePlanningProposalResponse(
    IReadOnlyList<RouteAssignmentResponse> Assignments,
    int DriverCount,
    int VehicleCount,
    int ShipmentCount,
    long TotalDistanceMeters,
    int TotalDurationMinutes,
    IReadOnlyList<ExcludedShipmentResponse> ExcludedShipments,
    IReadOnlyList<ExcludedDriverResponse> ExcludedDrivers);

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

/// <summary>
/// A selected driver that was left out of the proposal, with the reason why and the score that
/// justified it. Not dropped silently: a caller that selected drivers and gets back a plan for
/// fewer of them needs to know which ones dropped out and on what grounds.
/// </summary>
/// <param name="Score">
/// Null when the driver was rejected before being scored, which is the case for insufficient
/// vehicle capacity.
/// </param>
public sealed record ExcludedDriverResponse(
    Guid DriverId,
    decimal? Score,
    DriverIneligibilityReason Reason,
    string Detail);