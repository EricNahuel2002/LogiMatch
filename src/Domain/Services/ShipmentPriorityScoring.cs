 using Domain.Enums;

namespace Domain.Services;

public sealed record ShipmentPriorityInput(
    Guid ShipmentId,
    double? WindowSpanMinutes,
    double? DistanceMeters,
    decimal WeightKg,
    int DeliveryFailedCount,
    int FinalizedCount);

public sealed record ShipmentPriorityResult(Guid ShipmentId, decimal Score, ShipmentPriority Priority);

public sealed record ShipmentScoreResult(Guid ShipmentId, decimal Score);

public static class ShipmentPriorityScoring
{
    public const decimal WindowSpanWeight = 4m;
    public const decimal DistanceWeight = 3m;
    public const decimal WeightWeight = 2m;
    public const decimal ClientAbsenceRateWeight = 2m;

    public const decimal TotalWeight =
        WindowSpanWeight
        + DistanceWeight
        + WeightWeight
        + ClientAbsenceRateWeight;

    public const decimal LowThreshold = 0.25m;
    public const decimal NormalThreshold = 0.5m;
    public const decimal HighThreshold = 0.75m;

    /// <summary>
    /// Urgency assigned to each priority level. The gaps are uneven because they mirror the
    /// arc cost factors the solver has always applied: Urgent halves the arc into its node,
    /// while Low multiplies it by 1.5. Linearizing on the enum ordinals instead would make
    /// Urgent and High equidistant, which is not what the solver ever did.
    /// </summary>
    public const decimal UrgentUrgency = 1.00m;
    public const decimal HighUrgency = 0.75m;
    public const decimal NormalUrgency = 0.50m;
    public const decimal LowUrgency = 0.00m;

    public static IReadOnlyList<ShipmentScoreResult> Assign(
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

        var minRate = inputs.Min(i => ClientAbsenceRate(i.DeliveryFailedCount, i.FinalizedCount));
        var maxRate = inputs.Max(i => ClientAbsenceRate(i.DeliveryFailedCount, i.FinalizedCount));

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

                var clientAbsenceRate = ClientAbsenceRate(i.DeliveryFailedCount, i.FinalizedCount);
                var absenceNormalized = NormalizeLower(clientAbsenceRate, minRate, maxRate);

                var weighted =
                    WindowSpanWeight * windowTightness
                    + DistanceWeight * distanceFactor
                    + WeightWeight * weightNormalized
                    + ClientAbsenceRateWeight * absenceNormalized;

                var score = Clamp(weighted / TotalWeight, 0m, 1m);

                return new ShipmentScoreResult(i.ShipmentId, score);
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.ShipmentId)
            .ToList();
    }

    /// <summary>
    /// Inverse of <see cref="ToPriority(decimal)" />: turns a stored priority into the 0..1
    /// urgency the routing solver works with. Kept next to it so the two directions cannot
    /// drift apart.
    /// </summary>
    /// <remarks>
    /// Only relative ordering and magnitude matter downstream, so the enum is linearized
    /// instead of being kept as is: it reaches the solver as a multiplicative cost factor and
    /// as the driver mismatch penalty weight, both of which need a continuous magnitude.
    /// </remarks>
    public static decimal UrgencyOf(ShipmentPriority priority) => priority switch
    {
        ShipmentPriority.Urgent => UrgentUrgency,
        ShipmentPriority.High => HighUrgency,
        ShipmentPriority.Normal => NormalUrgency,
        ShipmentPriority.Low => LowUrgency,
        _ => NormalUrgency
    };

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

        return ShipmentPriority.High;
    }

    public static decimal ClientAbsenceRate(int deliveryFailedCount, int finalizedCount)
    {
        if (finalizedCount == 0)
        {
            return deliveryFailedCount > 0 ? 1m : 0m;
        }

        return deliveryFailedCount / (decimal)finalizedCount;
    }

    public static int PriorityToRankIndex(ShipmentPriority priority, int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        return priority switch
        {
            ShipmentPriority.Urgent => 0,
            ShipmentPriority.High => (int)Math.Floor((count - 1) * 0.25d),
            ShipmentPriority.Normal => (int)Math.Floor((count - 1) * 0.5d),
            ShipmentPriority.Low => count - 1,
            _ => 0
        };
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