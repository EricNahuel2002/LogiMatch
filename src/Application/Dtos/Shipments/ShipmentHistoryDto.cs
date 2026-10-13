using Domain.Enums;

namespace Application.Dtos.Shipments;

public record ShipmentHistoryDto(
    Guid Id,
    Guid ShipmentId,
    ShipmentStatus Status,
    Guid? RouteStopId,
    string? Note,
    DateTime RecordedAt);
