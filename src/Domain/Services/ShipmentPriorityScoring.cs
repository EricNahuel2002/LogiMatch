using Domain.Enums;

namespace Domain.Services;

public sealed record ShipmentPriorityInput(
    Guid ShipmentId,
    double? WindowSpanMinutes,
    double? DistanceMeters,
    decimal WeightKg,
    int AbsentDeliveriesCount,
    int SucceededDeliveriesCount);

public sealed record ShipmentPriorityResult(Guid ShipmentId, decimal Score, ShipmentPriority Priority);

public static class ShipmentPriorityScoring
{
    public const decimal WindowSpanWeight = 4m;
    public const decimal DistanceWeight = 3m;
    public const decimal WeightWeight = 2m;
    public const decimal AbsenceRateWeight = 2m;

    public const decimal TotalWeight =
        WindowSpanWeight
        + DistanceWeight
        + WeightWeight
        + AbsenceRateWeight;

    public const decimal LowThreshold = 0.25m;
    public const decimal NormalThreshold = 0.5m;
    public const decimal HighThreshold = 0.75m;

    public static IReadOnlyList<ShipmentPriorityResult> Assign(
        IReadOnlyList<ShipmentPriorityInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return [];
        }

        var spans = inputs
            .Select(i => i.WindowSpanMinutes)
            .Where(s => s.HasValue)
            .Cast<double>()
            .ToList();
        var minSpan = spans.Count > 0 ? spans.Min() : 0d;
        var maxSpan = spans.Count > 0 ? spans.Max() : 0d;

        var distances = inputs
            .Select(i => i.DistanceMeters)
            .Where(d => d.HasValue)
            .Cast<double>()
            .ToList();
        var minDistance = distances.Count > 0 ? distances.Min() : 0d;
        var maxDistance = distances.Count > 0 ? distances.Max() : 0d;

        var minWeight = inputs.Min(i => i.WeightKg);
        var maxWeight = inputs.Max(i => i.WeightKg);

        var minRate = inputs.Min(i => AbsentRate(i.AbsentDeliveriesCount, i.SucceededDeliveriesCount));
        var maxRate = inputs.Max(i => AbsentRate(i.AbsentDeliveriesCount, i.SucceededDeliveriesCount));

        return inputs
            .Select(i =>
            {
                var span = i.WindowSpanMinutes ?? maxSpan;
                var windowTightness = NormalizeLower((decimal)span, (decimal)minSpan, (decimal)maxSpan);

                var distanceNormalized = i.DistanceMeters is { } distance
                    ? NormalizeHigher((decimal)distance, (decimal)minDistance, (decimal)maxDistance)
                    : 0.5m;

                var distanceFactor = i.DistanceMeters is null
                    ? 0m
                    : distanceNormalized * (2m * windowTightness - 1m);

                var weightNormalized = NormalizeHigher(i.WeightKg, minWeight, maxWeight);

                var absentRate = AbsentRate(i.AbsentDeliveriesCount, i.SucceededDeliveriesCount);
                var absenceNormalized = NormalizeLower(absentRate, minRate, maxRate);

                var weighted =
                    WindowSpanWeight * windowTightness
                    + DistanceWeight * distanceFactor
                    + WeightWeight * weightNormalized
                    + AbsenceRateWeight * absenceNormalized;

                var score = Clamp(weighted / TotalWeight, 0m, 1m);

                return new ShipmentPriorityResult(i.ShipmentId, score, ToPriority(score));
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.ShipmentId)
            .ToList();
    }

    public static ShipmentPriority ToPriority(decimal score)
    {
        if (score < LowThreshold)
        {
            return ShipmentPriority.Low;
        }

        if (score < NormalThreshold)
        {
            return ShipmentPriority.Normal;
        }

        if (score < HighThreshold)
        {
            return ShipmentPriority.High;
        }

        return ShipmentPriority.Urgent;
    }

    public static decimal AbsentRate(int absentDeliveriesCount, int succeededDeliveriesCount)
    {
        if (succeededDeliveriesCount == 0)
        {
            return absentDeliveriesCount > 0 ? 1m : 0m;
        }

        return absentDeliveriesCount / (decimal)succeededDeliveriesCount;
    }

    private static decimal NormalizeHigher(decimal value, decimal min, decimal max)
    {
        return max == min ? 0.5m : (value - min) / (max - min);
    }

    private static decimal NormalizeLower(decimal value, decimal min, decimal max)
    {
        return max == min ? 0.5m : (max - value) / (max - min);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}