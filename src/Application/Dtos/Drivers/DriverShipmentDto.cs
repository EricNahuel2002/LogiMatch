using Domain.Enums;

namespace Application.Dtos.Drivers;

public record DriverShipmentDto(
    Guid ShipmentId,
    ShipmentStatus Status,
    ShipmentPriority Priority,
    Guid OrderId,
    DateTime CreatedAt);
