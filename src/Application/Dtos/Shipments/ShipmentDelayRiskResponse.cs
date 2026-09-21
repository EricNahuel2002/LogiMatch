using Domain.Enums;

namespace Application.Dtos.Shipments;

public sealed record ShipmentDriverResponse(Guid DriverId, string Name, string Surname);

public sealed record ShipmentDelayRiskResponse(
    Guid ShipmentId,
    ShipmentStatus Status,
    ShipmentPriority Priority,
    decimal? DelayRiskPercentage,
    ShipmentDriverResponse? Driver);