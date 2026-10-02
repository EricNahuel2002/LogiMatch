using Domain.Enums;
using Domain.ValueObjects;

namespace Application.Persistence;

/// <summary>
/// Read model for the route planner. Carries only what the solver needs, so a planning
/// preview never has to materialise the full <see cref="Domain.Entities.Shipment" /> graph.
/// </summary>
/// <param name="Destination">
/// Delivery coordinate, or null when the shipment has no route stop yet and therefore no
/// geocoded address.
/// </param>
/// <param name="DeliveryWindowStartAt">Local order window start, or null when unbounded.</param>
/// <param name="DeliveryWindowEndAt">Local order window end, or null when unbounded.</param>
public sealed record ShipmentPlanningData(
    Guid ShipmentId,
    ShipmentPriority Priority,
    decimal WeightKg,
    Coordinate? Destination,
    DateTime? DeliveryWindowStartAt,
    DateTime? DeliveryWindowEndAt);
