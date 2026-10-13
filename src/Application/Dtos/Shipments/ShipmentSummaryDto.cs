namespace Application.Dtos.Shipments;

public record ShipmentSummaryDto(
    Guid Id,
    Domain.Enums.ShipmentStatus Status,
    Domain.Enums.ShipmentPriority Priority,
    decimal? DelayRiskPercentage,
    Guid OrderId,
    Guid? AssignedDriverId,
    Guid? RouteStopId,
    Guid? RouteId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
