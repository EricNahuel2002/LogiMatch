using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Persistence;

public sealed record PendingShipmentPriorityData(
    Shipment Shipment,
    Coordinate? RouteOrigin,
    Coordinate? Destination,
    decimal WeightKg,
    double? WindowSpanMinutes,
    int DeliveryFailedCount,
    int FinalizedCount);