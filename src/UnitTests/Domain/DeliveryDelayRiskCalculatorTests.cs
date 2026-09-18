using Domain.Services;

namespace UnitTests.Domain;

public class DeliveryDelayRiskCalculatorTests
{
    private static readonly DateTime ArrivalAt = new(2026, 9, 17, 15, 0, 0);

    [Fact]
    public void CalculateWindowScore_WhenMoreThan30MinutesLeft_ReturnsZero()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(40), ArrivalAt);

        Assert.Equal(0m, score);
    }

    [Fact]
    public void CalculateWindowScore_WhenExactly30MinutesLeft_Returns15()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(30), ArrivalAt);

        Assert.Equal(15m, score);
    }

    [Fact]
    public void CalculateWindowScore_WhenBetween20And30MinutesLeft_Returns15()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(25), ArrivalAt);

        Assert.Equal(15m, score);
    }

    [Fact]
    public void CalculateWindowScore_WhenExactly20MinutesLeft_Returns30()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(20), ArrivalAt);

        Assert.Equal(30m, score);
    }

    [Fact]
    public void CalculateWindowScore_WhenBetween10And20MinutesLeft_Returns30()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(15), ArrivalAt);

        Assert.Equal(30m, score);
    }

    [Fact]
    public void CalculateWindowScore_WhenExactly10MinutesLeft_Returns45()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(10), ArrivalAt);

        Assert.Equal(45m, score);
    }

    [Fact]
    public void CalculateWindowScore_WhenLessThan10MinutesLeft_Returns45()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(5), ArrivalAt);

        Assert.Equal(45m, score);
    }

    [Fact]
    public void CalculateWindowScore_WhenWindowAlreadyPassed_Returns45()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(
            ArrivalAt.AddMinutes(-15), ArrivalAt);

        Assert.Equal(45m, score);
    }

    [Fact]
    public void CalculateWindowScore_WithoutWindow_ReturnsZero()
    {
        var score = DeliveryDelayRiskCalculator.CalculateWindowScore(null, ArrivalAt);

        Assert.Equal(0m, score);
    }

    [Fact]
    public void CalculateSpeedScore_When60KphOrLess_ReturnsZero()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(60, 60m);

        Assert.Equal(0m, score);
    }

    [Fact]
    public void CalculateSpeedScore_Between60And70_Returns11()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(60, 66m);

        Assert.Equal(11m, score);
    }

    [Fact]
    public void CalculateSpeedScore_AtExactly70_Returns11()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(60, 70m);

        Assert.Equal(11m, score);
    }

    [Fact]
    public void CalculateSpeedScore_Between70And80_Returns22()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(60, 75m);

        Assert.Equal(22m, score);
    }

    [Fact]
    public void CalculateSpeedScore_AtExactly80_Returns22()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(60, 80m);

        Assert.Equal(22m, score);
    }

    [Fact]
    public void CalculateSpeedScore_Above80_Returns33()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(60, 90m);

        Assert.Equal(33m, score);
    }

    [Fact]
    public void CalculateSpeedScore_WithNoArrivalTime_ReturnsZero()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(0, 50m);

        Assert.Equal(0m, score);
    }

    [Fact]
    public void CalculateSpeedScore_Example30KmIn25Minutes_Returns22()
    {
        var score = DeliveryDelayRiskCalculator.CalculateSpeedScore(25, 30m);

        Assert.Equal(22m, score);
    }

    [Fact]
    public void CalculateQueueScore_WithNoShipmentsAhead_ReturnsZero()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(0, TimeSpan.FromMinutes(10));

        Assert.Equal(0m, score);
    }

    [Fact]
    public void CalculateQueueScore_WithoutAverage_ReturnsZero()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(5, null);

        Assert.Equal(0m, score);
    }

    [Fact]
    public void CalculateQueueScore_Below15Minutes_ReturnsZero()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(1, TimeSpan.FromMinutes(10));

        Assert.Equal(0m, score);
    }

    [Fact]
    public void CalculateQueueScore_AtExactly15Minutes_Returns733()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(3, TimeSpan.FromMinutes(5));

        Assert.Equal(7.33m, score);
    }

    [Fact]
    public void CalculateQueueScore_Between15And25Minutes_Returns733()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(2, TimeSpan.FromMinutes(10));

        Assert.Equal(7.33m, score);
    }

    [Fact]
    public void CalculateQueueScore_Between25And40Minutes_Returns1466()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(6, TimeSpan.FromMinutes(5));

        Assert.Equal(14.66m, score);
    }

    [Fact]
    public void CalculateQueueScore_AtExactly25Minutes_Returns1466()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(5, TimeSpan.FromMinutes(5));

        Assert.Equal(14.66m, score);
    }

    [Fact]
    public void CalculateQueueScore_At40MinutesOrMore_Returns22()
    {
        var score = DeliveryDelayRiskCalculator.CalculateQueueScore(4, TimeSpan.FromMinutes(10));

        Assert.Equal(22m, score);
    }

    [Fact]
    public void Evaluate_SumsScoresAcrossVariants()
    {
        var input = new DeliveryDelayRiskInput(
            ArrivalAt.AddMinutes(25),
            ArrivalAt,
            24,
            30m,
            2,
            TimeSpan.FromMinutes(10));

        var result = DeliveryDelayRiskCalculator.Evaluate(input);

        Assert.Equal(15m, result.WindowScore);
        Assert.Equal(22m, result.SpeedScore);
        Assert.Equal(7.33m, result.QueueScore);
        Assert.Equal(44.33m, result.RiskIndex);
    }

    [Fact]
    public void Evaluate_WithoutWindowAndAverage_StillCountsSpeed()
    {
        var input = new DeliveryDelayRiskInput(
            null,
            ArrivalAt,
            20,
            30m,
            3,
            null);

        var result = DeliveryDelayRiskCalculator.Evaluate(input);

        Assert.Equal(0m, result.WindowScore);
        Assert.Equal(33m, result.SpeedScore);
        Assert.Equal(0m, result.QueueScore);
        Assert.Equal(33m, result.RiskIndex);
    }

    [Fact]
    public void Evaluate_MaximumIndexIs100()
    {
        var input = new DeliveryDelayRiskInput(
            ArrivalAt.AddMinutes(5),
            ArrivalAt,
            60,
            90m,
            4,
            TimeSpan.FromMinutes(10));

        var result = DeliveryDelayRiskCalculator.Evaluate(input);

        Assert.Equal(100m, result.RiskIndex);
    }
}