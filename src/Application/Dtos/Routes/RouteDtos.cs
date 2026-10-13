using Domain.Enums;
using Domain.ValueObjects;

namespace Application.Dtos.Routes;

public record RouteStopSummaryDto(
    Guid Id,
    Guid RouteId,
    Guid ShipmentId,
    string? Name,
    int StopOrder,
    decimal Latitude,
    decimal Longitude,
    decimal DistanceMeters,
    decimal TollCost);

public record RouteSummaryDto(
    Guid Id,
    bool IsActive,
    RouteCancellationReason? CancellationReason,
    Guid? VehicleId,
    Guid? DriverId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record RouteDetailsDto(
    Guid Id,
    bool IsActive,
    RouteCancellationReason? CancellationReason,
    Guid? VehicleId,
    Guid? DriverId,
    Coordinate Origin,
    RouteStopSummaryDto[] Stops,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
