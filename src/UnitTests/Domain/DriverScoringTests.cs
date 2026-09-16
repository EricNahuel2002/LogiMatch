using Domain.Services;

namespace UnitTests.Domain;

public class DriverScoringTests
{
    [Fact]
    public void Rank_WithSingleCandidate_UsesNeutralNormalization()
    {
        var input = new DriverScoringInput(Guid.NewGuid(), 1, 0, 0, 1000, 30, 500m);

        var ranked = DriverScoring.Rank([input]);

        var result = Assert.Single(ranked);
        Assert.Equal(input.DriverId, result.DriverId);
        Assert.Equal(
            0.5m,
            result.Score);
    }

    [Fact]
    public void Rank_CloserDriver_WinsWhenOtherMetricsAreEqual()
    {
        var driverA = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 2000, 30, 100m);
        var driverB = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 500, 10, 100m);

        var ranked = DriverScoring.Rank([driverA, driverB]);

        Assert.Equal(driverB.DriverId, ranked[0].DriverId);
    }

    [Fact]
    public void Rank_MoreSuccessfulAttempts_WinsWhenDistanceIsEqual()
    {
        var driverA = new DriverScoringInput(Guid.NewGuid(), 5, 0, 0, 1000, 30, 100m);
        var driverB = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 30, 100m);

        var ranked = DriverScoring.Rank([driverA, driverB]);

        Assert.Equal(driverA.DriverId, ranked[0].DriverId);
    }

    [Fact]
    public void Rank_BusierDriver_LosesWhenOtherMetricsAreEqual()
    {
        var freeDriver = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 30, 100m);
        var busyDriver = new DriverScoringInput(Guid.NewGuid(), 0, 2, 1, 1000, 30, 100m);

        var ranked = DriverScoring.Rank([freeDriver, busyDriver]);

        Assert.Equal(freeDriver.DriverId, ranked[0].DriverId);
    }

    [Fact]
    public void Rank_CheaperOperationDriver_WinsWhenOtherMetricsAreEqual()
    {
        var expensive = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 30, 100m, 500m);
        var cheap = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 30, 100m, 100m);

        var ranked = DriverScoring.Rank([expensive, cheap]);

        Assert.Equal(cheap.DriverId, ranked[0].DriverId);
    }

    [Fact]
    public void Rank_OperationCostIsFirstTieBreak()
    {
        var cheapLowSuccess = new DriverScoringInput(Guid.NewGuid(), 0, 1, 1, 1000, 30, 100m, 100m);
        var pricierHighSuccess = new DriverScoringInput(Guid.NewGuid(), 10, 1, 1, 1000, 30, 100m, 420m);
        var middle = new DriverScoringInput(Guid.NewGuid(), 5, 1, 1, 1000, 30, 100m, 900m);

        var ranked = DriverScoring.Rank([cheapLowSuccess, pricierHighSuccess, middle]);

        Assert.Equal(ranked[1].Score, ranked[0].Score);
        Assert.Equal(cheapLowSuccess.DriverId, ranked[0].DriverId);
        Assert.Equal(pricierHighSuccess.DriverId, ranked[1].DriverId);
    }

    [Fact]
    public void OperationCostWeight_EqualsDistanceWeight()
    {
        Assert.Equal(DriverScoring.DistanceWeight, DriverScoring.OperationCostWeight);
        Assert.Equal(5m, DriverScoring.OperationCostWeight);
    }

    [Fact]
    public void Rank_WithNoCandidates_ReturnsEmpty()
    {
        var ranked = DriverScoring.Rank([]);

        Assert.Empty(ranked);
    }

    [Fact]
    public void Rank_TieBreakByDistance_ThenByLoad_ThenById()
    {
        var idLow = Guid.NewGuid();
        var idHigh = Guid.NewGuid();

        var tied = new DriverScoringInput(idHigh, 0, 0, 0, 500, 10, 100m);
        var near = new DriverScoringInput(idLow, 0, 0, 0, 400, 10, 100m);

        var ranked = DriverScoring.Rank([tied, near]);

        Assert.Equal(near.DriverId, ranked[0].DriverId);
    }

    [Fact]
    public void Rank_FasterDriver_WinsWhenOtherMetricsAreEqual()
    {
        var slowDriver = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 30, 100m);
        var fastDriver = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 10, 100m);

        var ranked = DriverScoring.Rank([slowDriver, fastDriver]);

        Assert.Equal(fastDriver.DriverId, ranked[0].DriverId);
    }

    [Fact]
    public void FilterFeasible_WhenArrivalWithinWindow_KeepsCandidate()
    {
        var now = new DateTime(2026, 9, 9, 9, 0, 0);
        var windowStart = now.AddMinutes(15);
        var windowEnd = now.AddMinutes(60);
        var onTime = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 30, 100m);

        var feasible = DriverScoring.FilterFeasible([onTime], now, windowStart, windowEnd);

        Assert.Equal(onTime.DriverId, Assert.Single(feasible).DriverId);
    }

    [Fact]
    public void FilterFeasible_WhenArrivalBeforeWindowStart_ExcludesCandidate()
    {
        var now = new DateTime(2026, 9, 9, 9, 0, 0);
        var windowStart = now.AddMinutes(30);
        var windowEnd = now.AddMinutes(60);
        var early = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 15, 100m);

        var feasible = DriverScoring.FilterFeasible([early], now, windowStart, windowEnd);

        Assert.Empty(feasible);
    }

    [Fact]
    public void FilterFeasible_WhenArrivalAfterWindowEnd_ExcludesCandidate()
    {
        var now = new DateTime(2026, 9, 9, 9, 0, 0);
        var windowStart = now.AddMinutes(-60);
        var windowEnd = now.AddMinutes(-30);
        var late = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 15, 100m);

        var feasible = DriverScoring.FilterFeasible([late], now, windowStart, windowEnd);

        Assert.Empty(feasible);
    }

    [Fact]
    public void FilterFeasible_ArrivalOnWindowBoundaries_KeepsCandidate()
    {
        var now = new DateTime(2026, 9, 9, 9, 0, 0);
        var windowStart = now.AddMinutes(15);
        var windowEnd = now.AddMinutes(30);
        var onTime = new DriverScoringInput(Guid.NewGuid(), 0, 0, 0, 1000, 15, 100m);

        var feasible = DriverScoring.FilterFeasible([onTime], now, windowStart, windowEnd);

        Assert.Equal(onTime.DriverId, Assert.Single(feasible).DriverId);
    }

    [Fact]
    public void FilterFeasible_WithNoCandidates_ReturnsEmpty()
    {
        var now = new DateTime(2026, 9, 9, 9, 0, 0);

        var feasible = DriverScoring.FilterFeasible([], now, now, now.AddHours(1));

        Assert.Empty(feasible);
    }
}