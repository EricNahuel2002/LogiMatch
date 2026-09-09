namespace Domain.Services;

public sealed record DriverScoringInput(
    Guid DriverId,
    int SuccessAttemptsToday,
    int PendingShipmentCount,
    int InProgressShipmentCount,
    int DistanceMeters,
    int DurationMinutes,
    decimal FreeCapacityKg);

public sealed record DriverScoringResult(Guid DriverId, decimal Score, DriverScoringInput Input);

public static class DriverScoring
{
    public const decimal SuccessAttemptsWeight = 2m;
    public const decimal PendingShipmentCountWeight = 1m;
    public const decimal InProgressShipmentCountWeight = 2m;
    public const decimal DistanceWeight = 5m;
    public const decimal DurationWeight = 5m;
    public const decimal FreeCapacityWeight = 1m;

    public const decimal TotalWeight =
        SuccessAttemptsWeight
        + PendingShipmentCountWeight
        + InProgressShipmentCountWeight
        + DistanceWeight
        + DurationWeight
        + FreeCapacityWeight;

    public static IReadOnlyList<DriverScoringResult> Rank(IReadOnlyList<DriverScoringInput> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return [];
        }

        var minSuccess = candidates.Min(c => c.SuccessAttemptsToday);
        var maxSuccess = candidates.Max(c => c.SuccessAttemptsToday);
        var minPending = candidates.Min(c => c.PendingShipmentCount);
        var maxPending = candidates.Max(c => c.PendingShipmentCount);
        var minInProgress = candidates.Min(c => c.InProgressShipmentCount);
        var maxInProgress = candidates.Max(c => c.InProgressShipmentCount);
        var minDistance = candidates.Min(c => c.DistanceMeters);
        var maxDistance = candidates.Max(c => c.DistanceMeters);
        var minDuration = candidates.Min(c => c.DurationMinutes);
        var maxDuration = candidates.Max(c => c.DurationMinutes);
        var minCapacity = candidates.Min(c => c.FreeCapacityKg);
        var maxCapacity = candidates.Max(c => c.FreeCapacityKg);

        return candidates
            .Select(c =>
            {
                var weighted =
                    SuccessAttemptsWeight * NormalizeHigher(c.SuccessAttemptsToday, minSuccess, maxSuccess)
                    + PendingShipmentCountWeight * NormalizeLower(c.PendingShipmentCount, minPending, maxPending)
                    + InProgressShipmentCountWeight * NormalizeLower(c.InProgressShipmentCount, minInProgress, maxInProgress)
                    + DistanceWeight * NormalizeLower(c.DistanceMeters, minDistance, maxDistance)
                    + DurationWeight * NormalizeLower(c.DurationMinutes, minDuration, maxDuration)
                    + FreeCapacityWeight * NormalizeHigher(c.FreeCapacityKg, minCapacity, maxCapacity);

                return new DriverScoringResult(c.DriverId, weighted / TotalWeight, c);
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Input.DistanceMeters)
            .ThenBy(r => r.Input.DurationMinutes)
            .ThenBy(r => r.Input.PendingShipmentCount + r.Input.InProgressShipmentCount)
            .ThenBy(r => r.DriverId)
            .ToList();
    }

    public static IReadOnlyList<DriverScoringInput> FilterFeasible(
        IReadOnlyList<DriverScoringInput> candidates,
        DateTime now,
        DateTime windowStartAt,
        DateTime windowEndAt)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(c => IsFeasible(c.DurationMinutes, now, windowStartAt, windowEndAt))
            .ToList();
    }

    public static bool IsFeasible(int travelMinutes, DateTime now, DateTime windowStartAt, DateTime windowEndAt)
    {
        var arrivalAt = now.AddMinutes(travelMinutes);
        return arrivalAt >= windowStartAt && arrivalAt <= windowEndAt;
    }

    private static decimal NormalizeHigher(decimal value, decimal min, decimal max)
    {
        return max == min ? 0.5m : (value - min) / (max - min);
    }

    private static decimal NormalizeLower(decimal value, decimal min, decimal max)
    {
        return max == min ? 0.5m : (max - value) / (max - min);
    }
}