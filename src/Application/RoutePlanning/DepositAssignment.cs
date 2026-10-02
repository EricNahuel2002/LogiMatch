using Domain.ValueObjects;

namespace Application.RoutePlanning;

/// <summary>
/// A driver that needs a deposit before the problem can be built.
/// </summary>
/// <param name="DriverId">Driver the deposit is for.</param>
/// <param name="CurrentLocation">Where the driver starts the route.</param>
/// <param name="RequestedDepositId">
/// Deposit the administrator pinned to this driver, or null when they left it to the planner.
/// </param>
public sealed record DepositAssignment(
    Guid DriverId,
    Coordinate CurrentLocation,
    Guid? RequestedDepositId);