namespace Application.Dtos.Drivers;

public record DriverRouteCurrentDto(
    Guid RouteId,
    bool IsActive,
    Guid? VehicleId,
    DateTime CreatedAt);
