using Domain.Enums;

namespace Application.Dtos.Drivers;

public record DriverRouteCurrentDto(
    Guid RouteId,
    bool IsActive,
    Guid? VehicleId,
    decimal OriginLatitude,
    decimal OriginLongitude,
    IReadOnlyList<DriverRouteStopDto> Stops,
    DateTime CreatedAt);

public record DriverRouteStopDto(
    Guid RouteStopId,
    Guid ShipmentId,
    int StopOrder,
    string? Name,
    decimal Latitude,
    decimal Longitude,
    ShipmentStatus Status,
    string? CustomerName,
    bool IsCurrent,
    IReadOnlyList<DriverOrderItemDto> Items);

public record DriverOrderItemDto(
    string Name,
    int Quantity,
    decimal Price);
