namespace Application.RoutePlanning;

/// <summary>
/// Capacity available to the vehicle a driver is planned to use.
/// </summary>
public sealed record PlanningVehicleData(
    Guid VehicleId,
    decimal CapacityKg,
    bool Active);
