using Domain.Enums;

namespace Application.Dtos.Shipments;

public record ShipmentOrderItemDto(
    Guid OrderItemId,
    string Name,
    int Quantity,
    decimal WeightKg);

public record ShipmentRouteStopDto(
    Guid? RouteStopId,
    int? StopOrder,
    Guid? RouteId,
    string? Name,
    decimal? Latitude,
    decimal? Longitude,
    decimal DistanceMeters,
    decimal TollCost);

public record ShipmentDriverDto(
    Guid? DriverId,
    string? Name,
    string? Surname);

public record ShipmentDetailsDto(
    Guid Id,
    ShipmentStatus Status,
    ShipmentPriority Priority,
    decimal? DelayRiskPercentage,
    bool ArrivedAtDestination,
    Guid OrderId,
    ShipmentOrderItemDto[] Items,
    ShipmentRouteStopDto? RouteStop,
    ShipmentDriverDto? AssignedDriver,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
