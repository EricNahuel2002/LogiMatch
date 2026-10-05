using Domain.ValueObjects;

namespace Application.RoutePlanning;

/// <summary>
/// A shipment the solver must visit exactly once, with everything it needs to reason
/// about. No domain entity crosses into the solver.
/// </summary>
/// <param name="Urgency">
/// How much the shipment matters on the 0..1 scale, derived from its stored priority. Replaces
/// the enum so the solver can weigh it continuously: as an enum it could only pick one of a
/// fixed set of arc multipliers, and it could not scale the driver mismatch penalty.
/// </param>
public sealed record PlanningShipmentData(
    Guid ShipmentId,
    decimal Urgency,
    decimal WeightKg,
    Coordinate Destination,
    TimeWindow? DeliveryWindow);