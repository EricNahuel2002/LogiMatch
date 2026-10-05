using Domain.Enums;
using Domain.Services;

namespace UnitTests.Domain;

public class DriverEligibilityTests
{
    private static readonly DriverScoringInput Input = new(
        Guid.Empty,
        SuccessAttemptsToday: 0,
        PendingShipmentCount: 0,
        InProgressShipmentCount: 0,
        DistanceMeters: 0,
        DurationMinutes: 0,
        FreeCapacityKg: 0m,
        EstimatedOperationCost: null);

    [Fact]
    public void Select_NoCandidates_KeepsNothing()
    {
        var outcome = DriverEligibility.Select([], Policy());

        Assert.Empty(outcome.Kept);
        Assert.Empty(outcome.Rejected);
    }

    [Fact]
    public void Select_AllAboveMinimum_KeepsEveryone()
    {
        var pool = Pool(("a", 0.90m), ("b", 0.70m), ("c", 0.40m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.25m, tolerance: 0.5m, minimumDrivers: 1));

        Assert.Equal(3, outcome.Kept.Count);
        Assert.Empty(outcome.Rejected);
    }

    [Fact]
    public void Select_BelowMinimum_IsRejectedAsBelowMinimum()
    {
        var pool = Pool(("a", 0.90m), ("b", 0.10m));

        var outcome = DriverEligibility.Select(pool.Ranked, Policy());

        var rejected = Assert.Single(outcome.Rejected);

        Assert.Equal(DriverIneligibilityReason.ScoreBelowMinimum, rejected.Reason);
        Assert.Equal(0.10m, rejected.Score);
        Assert.Equal(pool.IdOf("b"), rejected.DriverId);
    }

    [Fact]
    public void Select_WithinToleranceOfBest_IsKept()
    {
        var pool = Pool(("a", 1.00m), ("b", 0.60m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0m, tolerance: 0.5m, minimumDrivers: 1));

        Assert.Equal(2, outcome.Kept.Count);
    }

    [Fact]
    public void Select_FarBelowBest_IsRejectedAsBelowTolerance()
    {
        var pool = Pool(("a", 1.00m), ("b", 0.20m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0m, tolerance: 0.5m, minimumDrivers: 1));

        var rejected = Assert.Single(outcome.Rejected);

        Assert.Equal(DriverIneligibilityReason.ScoreBelowTolerance, rejected.Reason);
        Assert.Equal(0.20m, rejected.Score);
        Assert.Equal(pool.IdOf("b"), rejected.DriverId);
    }

    [Fact]
    public void Select_ToleranceComparedAgainstRealBest_NotThePerfectScore()
    {
        // Best scores 0.60, nobody reaches 1.0. The 0.40 driver sits 0.20 below the best, so
        // it survives a 0.25 tolerance even though it would fail if the cutoff were measured
        // against a hypothetical perfect score.
        var pool = Pool(("a", 0.60m), ("b", 0.40m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0m, tolerance: 0.25m, minimumDrivers: 1));

        Assert.Equal(2, outcome.Kept.Count);
    }

    [Fact]
    public void Select_MinimumScore_TrumpsTolerance()
    {
        // The 0.70 driver is within tolerance of the best, but the absolute floor wins.
        var pool = Pool(("a", 0.90m), ("b", 0.70m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.75m, tolerance: 0.5m, minimumDrivers: 1));

        var rejected = Assert.Single(outcome.Rejected);

        Assert.Equal(DriverIneligibilityReason.ScoreBelowMinimum, rejected.Reason);
        Assert.Equal(pool.IdOf("b"), rejected.DriverId);
    }

    [Fact]
    public void Select_ScoreExactlyAtMinimum_IsKept()
    {
        var pool = Pool(("a", 0.90m), ("b", 0.25m));

        // Tolerance wide enough not to interfere: the point is the floor being inclusive.
        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.25m, tolerance: 0.7m, minimumDrivers: 1));

        Assert.Equal(2, outcome.Kept.Count);
    }

    [Fact]
    public void Select_ScoreExactlyAtToleranceBoundary_IsKept()
    {
        var pool = Pool(("a", 0.90m), ("b", 0.40m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0m, tolerance: 0.5m, minimumDrivers: 1));

        Assert.Equal(2, outcome.Kept.Count);
    }

    [Fact]
    public void Select_MinimumDrivers_PromotesBestOfTheRejected()
    {
        var pool = Pool(("a", 0.90m), ("b", 0.80m), ("c", 0.70m), ("d", 0.10m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.75m, tolerance: 0.1m, minimumDrivers: 3));

        Assert.Equal(3, outcome.Kept.Count);
        Assert.Equal([0.90m, 0.80m, 0.70m], outcome.Kept.Select(k => k.Score));

        var rejected = Assert.Single(outcome.Rejected);

        Assert.Equal(pool.IdOf("d"), rejected.DriverId);
    }

    [Fact]
    public void Select_MinimumDrivers_KeepsRankOrderAfterPromotion()
    {
        var pool = Pool(("a", 0.90m), ("b", 0.10m), ("c", 0.20m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.50m, tolerance: 0.1m, minimumDrivers: 2));

        Assert.Equal([0.90m, 0.20m], outcome.Kept.Select(k => k.Score));
    }

    [Fact]
    public void Select_MinimumDrivers_Zero_KeepsNothing()
    {
        var pool = Pool(("a", 0.90m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.95m, tolerance: 0m, minimumDrivers: 0));

        Assert.Empty(outcome.Kept);
        Assert.Single(outcome.Rejected);
    }

    [Fact]
    public void Select_EveryoneBelowMinimum_StillKeepsTheBest()
    {
        var pool = Pool(("a", 0.20m), ("b", 0.10m));

        var outcome = DriverEligibility.Select(pool.Ranked, Policy());

        var kept = Assert.Single(outcome.Kept);

        Assert.Equal(0.20m, kept.Score);
        Assert.Equal(pool.IdOf("a"), kept.DriverId);
    }

    [Fact]
    public void Select_NegativeMinimumDrivers_Throws()
    {
        var pool = Pool(("a", 0.5m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DriverEligibility.Select(pool.Ranked, Policy(minimumDrivers: -1)));
    }

    [Fact]
    public void Select_NullRanked_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DriverEligibility.Select(null!, Policy()));
    }

    [Fact]
    public void Select_NullPolicy_Throws()
    {
        var pool = Pool(("a", 0.5m));

        Assert.Throws<ArgumentNullException>(() =>
            DriverEligibility.Select(pool.Ranked, null!));
    }

    [Fact]
    public void Select_RejectionDetail_ExplainsTheThreshold()
    {
        var pool = Pool(("a", 0.90m), ("b", 0.10m));

        var outcome = DriverEligibility.Select(pool.Ranked, Policy());

        var rejected = Assert.Single(outcome.Rejected);

        Assert.Contains("0.25", rejected.Detail);
    }

    [Fact]
    public void Select_ScoreRejectedDriver_IsPromotedWhenKeptFleetCannotCarryThePlan()
    {
        // The case a real preview hit: the best scored driver alone cannot carry the work, so
        // dropping the other one turns a solvable plan into an infeasible one.
        var pool = CapacityPool(("a", 0.81m, 1500m), ("b", 0.19m, 2000m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.25m, tolerance: 0.5m, minimumDrivers: 1),
            requiredWeightKg: 3440m);

        Assert.Equal(2, outcome.Kept.Count);
        Assert.Empty(outcome.Rejected);
    }

    [Fact]
    public void Select_ScoreRejectedDriver_StaysOutWhenKeptFleetAlreadyCarriesThePlan()
    {
        // Same pool shape as above, but the best driver can carry it alone, so the threshold is
        // allowed to stand.
        var pool = CapacityPool(("a", 0.81m, 2000m), ("b", 0.19m, 1000m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.25m, tolerance: 0.5m, minimumDrivers: 1),
            requiredWeightKg: 1800m);

        Assert.Equal(pool.IdOf("a"), Assert.Single(outcome.Kept).DriverId);
        Assert.Equal(pool.IdOf("b"), Assert.Single(outcome.Rejected).DriverId);
    }

    [Fact]
    public void Select_PromotesAsManyAsTheWeightNeeds_InRankOrder()
    {
        var pool = CapacityPool(("a", 0.90m, 1000m), ("b", 0.30m, 1000m), ("c", 0.10m, 1000m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.35m, tolerance: 0.7m, minimumDrivers: 1),
            requiredWeightKg: 2500m);

        Assert.Equal(3, outcome.Kept.Count);
        Assert.Equal([0.90m, 0.30m, 0.10m], outcome.Kept.Select(k => k.Score));
        Assert.Empty(outcome.Rejected);
    }

    [Fact]
    public void Select_MinimumDrivers_StillAppliesWhenTheWeightIsAlreadyCovered()
    {
        var pool = CapacityPool(("a", 0.90m, 5000m), ("b", 0.10m, 5000m), ("c", 0.80m, 5000m));

        var outcome = DriverEligibility.Select(
            pool.Ranked,
            Policy(minimumScore: 0.85m, tolerance: 0.1m, minimumDrivers: 2),
            requiredWeightKg: 100m);

        Assert.Equal(2, outcome.Kept.Count);
        Assert.Equal([0.90m, 0.80m], outcome.Kept.Select(k => k.Score));
    }

    [Fact]
    public void Select_PoolThatCannotCarryTheWeightEvenTogether_KeepsEveryone()
    {
        // The shortfall is a fact of the fleet rather than something the thresholds caused, so
        // the whole pool stays and the solver is the one that reports it.
        var pool = CapacityPool(("a", 0.90m, 500m), ("b", 0.10m, 500m));

        var outcome = DriverEligibility.Select(pool.Ranked, Policy(), requiredWeightKg: 5000m);

        Assert.Equal(2, outcome.Kept.Count);
        Assert.Empty(outcome.Rejected);
    }

    [Fact]
    public void Select_NoRequiredWeight_MatchesOmittingTheArgument()
    {
        var pool = CapacityPool(("a", 0.90m, 1500m), ("b", 0.10m, 2000m));

        var explicitZero = DriverEligibility.Select(pool.Ranked, Policy(), requiredWeightKg: 0m);
        var omitted = DriverEligibility.Select(pool.Ranked, Policy());

        Assert.Equal(
            omitted.Kept.Select(k => k.DriverId),
            explicitZero.Kept.Select(k => k.DriverId));
        Assert.Equal(omitted.Rejected.Count, explicitZero.Rejected.Count);
    }

    [Fact]
    public void Select_NegativeRequiredWeight_BehavesLikeNoWeight()
    {
        var pool = CapacityPool(("a", 0.90m, 1500m), ("b", 0.10m, 2000m));

        var outcome = DriverEligibility.Select(pool.Ranked, Policy(), requiredWeightKg: -5m);

        Assert.Single(outcome.Kept);
        Assert.Single(outcome.Rejected);
    }

    private static DriverEligibilityPolicy Policy(
        decimal minimumScore = 0.25m,
        decimal tolerance = 0.5m,
        int minimumDrivers = 1) =>
        new(minimumScore, tolerance, minimumDrivers);

    /// <summary>
    /// Builds the already descending pool <see cref="DriverScoring.Rank" /> would return,
    /// keeping a name lookup so assertions can talk about drivers instead of guids.
    /// </summary>
    private static TestPool Pool(params (string Name, decimal Score)[] candidates)
    {
        var ids = candidates.ToDictionary(c => c.Name, _ => Guid.NewGuid());

        var ranked = candidates
            .OrderByDescending(c => c.Score)
            .Select(c => new DriverScoringResult(ids[c.Name], c.Score, Input))
            .ToList();

        return new TestPool(ranked, id => ids[id]);
    }

    /// <summary>
    /// Same as <see cref="Pool" /> but with a free capacity per driver, which is what decides how
    /// many of them the plan needs.
    /// </summary>
    private static TestPool CapacityPool(
        params (string Name, decimal Score, decimal FreeCapacityKg)[] candidates)
    {
        var ids = candidates.ToDictionary(c => c.Name, _ => Guid.NewGuid());

        var ranked = candidates
            .OrderByDescending(c => c.Score)
            .Select(c => new DriverScoringResult(
                ids[c.Name],
                c.Score,
                Input with { FreeCapacityKg = c.FreeCapacityKg }))
            .ToList();

        return new TestPool(ranked, id => ids[id]);
    }

    private sealed record TestPool(
        IReadOnlyList<DriverScoringResult> Ranked,
        Func<string, Guid> IdOf);
}