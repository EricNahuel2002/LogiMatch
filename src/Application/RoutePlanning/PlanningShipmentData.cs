using Domain.Enums;
using Domain.ValueObjects;

namespace Application.RoutePlanning;

/// <summary>
/// A shipment the solver must visit exactly once, with everything it needs to reason
/// about. No domain entity crosses into the solver.
/// </summary>
public sealed record PlanningShipmentData(
    Guid ShipmentId,
    ShipmentPriority Priority,
    decimal WeightKg,
    Coordinate Destination,
    TimeWindow? DeliveryWindow);
