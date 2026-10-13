namespace Application.Dtos.Vehicles;

public record VehicleSummaryDto(
    Guid Id,
    string LicensePlate,
    string? Brand,
    string? Model,
    decimal CapacityKg,
    bool Active,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
