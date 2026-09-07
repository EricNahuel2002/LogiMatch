using Domain.ValueObjects;

namespace Application.Persistence;

public sealed record DriverAssignmentCandidate(
    Guid DriverId,
    Coordinate? CurrentLocation,
    decimal MaxActiveVehicleCapacityKg,
    int SuccessAttemptsToday,
    int PendingShipmentCount,
    int InProgressShipmentCount,
    decimal InProgressShipmentWeightKg);