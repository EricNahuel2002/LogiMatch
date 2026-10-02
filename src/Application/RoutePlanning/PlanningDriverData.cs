using Domain.ValueObjects;

namespace Application.RoutePlanning;

/// <summary>
/// A driver-vehicle-deposit selection. The driver to vehicle pairing is decided by the
/// administrator, not by the solver: the solver only decides which shipments each vehicle serves
/// and in what order. The deposit is either the one the administrator pinned or the one
/// <see cref="IDepositAssignmentPolicy" /> chose, and it is always an active deposit.
/// </summary>
public sealed record PlanningDriverData(
    Guid DriverId,
    Coordinate CurrentLocation,
    Guid VehicleId,
    Guid DepositId);
