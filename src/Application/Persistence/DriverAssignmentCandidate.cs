using Domain.ValueObjects;

namespace Application.Persistence;

public sealed record DriverAssignmentCandidate(
    Guid DriverId,
    Coordinate? CurrentLocation,
    decimal MaxActiveVehicleCapacityKg,
    int SuccessAttemptsToday,
    int PendingShipmentCount,
    int InProgressShipmentCount,
    decimal InProgressShipmentWeightKg,
    decimal SalaryPerHour = 0m,
    decimal KilometersPerDay = 0m,
    decimal VehicleFuelConsumption = 0m,
    decimal VehicleFuelPrice = 0m,
    decimal VehicleMaintenanceCost = 0m,
    decimal ActiveRouteTollCost = 0m);