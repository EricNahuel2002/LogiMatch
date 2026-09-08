namespace Application.Dtos.Shipments;

public sealed record DriverRankingItem(
    Guid DriverId,
    decimal Score,
    int SuccessAttemptsToday,
    int PendingShipmentCount,
    int InProgressShipmentCount,
    int DistanceMeters,
    int DurationMinutes,
    decimal FreeCapacityKg);

public sealed record DriverAssignmentSuggestion(
    Guid ShipmentId,
    Guid RecommendedDriverId,
    IReadOnlyList<DriverRankingItem> Ranking);