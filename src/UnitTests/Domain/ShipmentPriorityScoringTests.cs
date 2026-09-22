using Domain.Enums;
using Domain.Services;

namespace UnitTests.Domain;

public class ShipmentPriorityScoringTests
{
    [Fact]
    public void Assign_WithNoCandidates_ReturnsEmpty()
    {
        var results = ShipmentPriorityScoring.Assign([]);

        Assert.Empty(results);
    }

    [Fact]
    public void Assign_SingleCandidate_GetsNormalPriority()
    {
        var input = new ShipmentPriorityInput(Guid.NewGuid(), null, null, 10m, 0, 0);

        var result = Assert.Single(ShipmentPriorityScoring.Assign([input]));

        Assert.Equal(input.ShipmentId, result.ShipmentId);
    }

    [Fact]
    public void Assign_TighterWindow_GetsHigherScore()
    {
        var tight = new ShipmentPriorityInput(Guid.NewGuid(), 60, null, 10m, 0, 1);
        var flexible = new ShipmentPriorityInput(Guid.NewGuid(), 600, null, 10m, 0, 1);

        var results = ShipmentPriorityScoring.Assign([flexible, tight]);

        Assert.Equal(tight.ShipmentId, results[0].ShipmentId);
        Assert.True(results[0].Score > results[1].Score);

    }

    [Fact]
    public void Assign_HeavierShipment_GetsHigherScore()
    {
        var light = new ShipmentPriorityInput(Guid.NewGuid(), 60, null, 10m, 0, 1);
        var heavy = new ShipmentPriorityInput(Guid.NewGuid(), 60, null, 100m, 0, 1);

        var results = ShipmentPriorityScoring.Assign([light, heavy]);

        Assert.Equal(heavy.ShipmentId, results[0].ShipmentId);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Assign_HigherAbsenceRate_GetsLowerScore()
    {
        var absent = new ShipmentPriorityInput(Guid.NewGuid(), 60, null, 100m, 2, 1);
        var clean = new ShipmentPriorityInput(Guid.NewGuid(), 60, null, 100m, 0, 1);

        var results = ShipmentPriorityScoring.Assign([absent, clean]);

        Assert.Equal(clean.ShipmentId, results[0].ShipmentId);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Assign_LongDistanceWithTightWindow_GetsHigherScoreThanWithFlexibleWindow()
    {
        var tight = new ShipmentPriorityInput(Guid.NewGuid(), 60, 5000, 10m, 0, 1);
        var flexible = new ShipmentPriorityInput(Guid.NewGuid(), 600, 5000, 10m, 0, 1);

        var results = ShipmentPriorityScoring.Assign([flexible, tight]);

        Assert.Equal(tight.ShipmentId, results[0].ShipmentId);
        Assert.True(results[0].Score > results[1].Score);

    }

    [Fact]
    public void Assign_WithoutDistance_DoesNotBenefitOrPenalize()
    {
        var withDistance = new ShipmentPriorityInput(Guid.NewGuid(), 60, 5000, 10m, 0, 1);
        var withoutDistance = new ShipmentPriorityInput(Guid.NewGuid(), 600, null, 10m, 0, 1);

        var results = ShipmentPriorityScoring.Assign([withDistance, withoutDistance]);

        Assert.True(results[0].Score > results[1].Score);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(0, 5, 0)]
    [InlineData(2, 4, 0.5)]
    [InlineData(5, 10, 0.5)]
    public void ClientAbsenceRate_ComputesQuotientWithGuard(
        int deliveryFailed,
        int finalized,
        decimal expected)
    {
        Assert.Equal(expected, ShipmentPriorityScoring.ClientAbsenceRate(deliveryFailed, finalized));
    }

    [Theory]
    [InlineData(ShipmentPriority.Urgent, 0, 0)]
    [InlineData(ShipmentPriority.Low, 1, 0)]
    [InlineData(ShipmentPriority.High, 5, 1)]
    [InlineData(ShipmentPriority.High, 9, 2)]
    [InlineData(ShipmentPriority.Normal, 5, 2)]
    [InlineData(ShipmentPriority.Normal, 10, 4)]
    [InlineData(ShipmentPriority.Low, 5, 4)]
    public void PriorityToRankIndex_MapsPriorityToRankingPosition(
        ShipmentPriority priority,
        int count,
        int expected)
    {
        Assert.Equal(expected, ShipmentPriorityScoring.PriorityToRankIndex(priority, count));
    }

    [Theory]
    [InlineData(0.00, ShipmentPriority.Low)]
    [InlineData(0.24, ShipmentPriority.Low)]
    [InlineData(0.25, ShipmentPriority.Normal)]
    [InlineData(0.49, ShipmentPriority.Normal)]
    [InlineData(0.50, ShipmentPriority.High)]
    [InlineData(0.74, ShipmentPriority.High)]
        [InlineData(0.75, ShipmentPriority.High)]
        [InlineData(1.00, ShipmentPriority.High)]
    public void ToPriority_MapsByThreshold(decimal score, ShipmentPriority expected)
    {
        Assert.Equal(expected, ShipmentPriorityScoring.ToPriority(score));
    }
}