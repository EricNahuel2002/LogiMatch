using System.Globalization;
using Domain.Enums;

namespace Domain.Services;

/// <summary>
/// Thresholds that decide which scored drivers are worth planning with.
/// </summary>
/// <param name="MinimumScore">
/// Absolute floor, on the 0..1 scale <see cref="DriverScoring.Rank" /> produces.
/// </param>
/// <param name="ScoreTolerance">
/// How far below the best scored driver a driver may fall and still be planned with.
/// </param>
/// <param name="MinimumDrivers">
/// Number of drivers kept when the thresholds would otherwise leave fewer. It is a floor, not a
/// target: the weight the plan has to carry can raise it, and it never resurrects a driver
/// rejected for any other reason.
/// </param>
public sealed record DriverEligibilityPolicy(
    decimal MinimumScore,
    decimal ScoreTolerance,
    int MinimumDrivers);

/// <summary>
/// A driver left out of a plan, with the reason and enough context to explain it.
/// </summary>
/// <param name="Score">
/// Null when the driver was rejected before scoring, which is the case for capacity: the
/// driver must not influence the normalization of the pool it would be compared against.
/// </param>
public sealed record IneligibleDriver(
    Guid DriverId,
    decimal? Score,
    DriverIneligibilityReason Reason,
    string Detail);

public sealed record DriverEligibilityOutcome(
    IReadOnlyList<DriverScoringResult> Kept,
    IReadOnlyList<IneligibleDriver> Rejected);

/// <summary>
/// Narrows a ranked pool of drivers down to the ones worth building a plan with.
/// </summary>
public static class DriverEligibility
{
    /// <summary>
    /// Applies <paramref name="policy" /> to <paramref name="ranked" />.
    /// </summary>
    /// <param name="requiredWeightKg">
    /// Total weight the plan has to carry. The thresholds express a preference, so they must not
    /// shrink the fleet below what can carry the work: drivers are promoted back until the free
    /// capacity of the kept ones covers this weight. Pass zero when there is nothing to carry.
    /// </param>
    public static DriverEligibilityOutcome Select(
        IReadOnlyList<DriverScoringResult> ranked,
        DriverEligibilityPolicy policy,
        decimal requiredWeightKg = 0m)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.MinimumDrivers < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policy),
                policy.MinimumDrivers,
                "Minimum drivers cannot be negative.");
        }

        var kept = new List<DriverScoringResult>(ranked.Count);
        var rejected = new List<IneligibleDriver>();

        if (ranked.Count == 0)
        {
            return new DriverEligibilityOutcome(kept, rejected);
        }

        var best = ranked[0];

        foreach (var candidate in ranked)
        {
            if (candidate.Score < policy.MinimumScore)
            {
                rejected.Add(new IneligibleDriver(
                    candidate.DriverId,
                    candidate.Score,
                    DriverIneligibilityReason.ScoreBelowMinimum,
                    $"Score {Format(candidate.Score)} is below the minimum {Format(policy.MinimumScore)}."));
                continue;
            }

            if (candidate.Score < best.Score - policy.ScoreTolerance)
            {
                rejected.Add(new IneligibleDriver(
                    candidate.DriverId,
                    candidate.Score,
                    DriverIneligibilityReason.ScoreBelowTolerance,
                    $"Score {Format(candidate.Score)} is more than {Format(policy.ScoreTolerance)} " +
                    $"below the best score {Format(best.Score)}."));
                continue;
            }

            kept.Add(candidate);
        }

        var requiredDrivers = Math.Max(
            policy.MinimumDrivers,
            RequiredDriverCount(ranked, requiredWeightKg));

        BackfillMinimum(kept, rejected, ranked, requiredDrivers);

        return new DriverEligibilityOutcome(kept, rejected);
    }

    /// <summary>
    /// How many of the highest ranked drivers are needed for their free capacity to cover
    /// <paramref name="requiredWeightKg" />. Zero when there is nothing to carry.
    /// </summary>
    private static int RequiredDriverCount(
        IReadOnlyList<DriverScoringResult> ranked,
        decimal requiredWeightKg)
    {
        if (requiredWeightKg <= 0m)
        {
            return 0;
        }

        var covered = 0m;
        var needed = 0;

        foreach (var candidate in ranked)
        {
            covered += candidate.Input.FreeCapacityKg;

            if (covered >= requiredWeightKg)
            {
                return needed + 1;
            }

            needed++;
        }

        // The pool as a whole cannot carry the plan. Promoting all of it is still the best answer
        // available here: the shortfall is a fact of the fleet, not something the thresholds
        // caused, so the solver reports it with the numbers behind it.
        return ranked.Count;
    }

    /// <summary>
    /// Promotes the highest ranked drivers until there are <paramref name="requiredDrivers" /> of
    /// them. Only score rejections are reconsidered: a driver turned down for a hard constraint
    /// stays out.
    /// </summary>
    private static void BackfillMinimum(
        List<DriverScoringResult> kept,
        List<IneligibleDriver> rejected,
        IReadOnlyList<DriverScoringResult> ranked,
        int requiredDrivers)
    {
        if (kept.Count >= requiredDrivers)
        {
            return;
        }

        var keptIds = kept.Select(c => c.DriverId).ToHashSet();
        var promotedAny = false;

        foreach (var candidate in ranked)
        {
            if (kept.Count >= requiredDrivers)
            {
                break;
            }

            if (!keptIds.Add(candidate.DriverId))
            {
                continue;
            }

            kept.Add(candidate);
            promotedAny = true;

            var entry = rejected.FindIndex(r =>
                r.DriverId == candidate.DriverId
                && r.Reason != DriverIneligibilityReason.InsufficientVehicleCapacity);

            if (entry >= 0)
            {
                rejected.RemoveAt(entry);
            }
        }

        // Promotions are appended out of rank order, so restore it: callers get the kept
        // drivers best first, and the report reads as a ranking rather than a set.
        if (promotedAny)
        {
            var rank = ranked
                .Select((candidate, index) => (candidate.DriverId, index))
                .ToDictionary(x => x.DriverId, x => x.index);

            kept.Sort((left, right) => rank[left.DriverId].CompareTo(rank[right.DriverId]));
        }
    }

    /// <summary>
    /// Details end up in an API response, so they must not change shape with the thread
    /// culture: under a comma decimal separator the thresholds would read as "0,25".
    /// </summary>
    private static string Format(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}