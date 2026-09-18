namespace Domain.Services;

public sealed record DeliveryDelayRiskInput(
    DateTime? DeliveryWindowEndAt,
    DateTime EstimatedArrivalAt,
    double ArrivalMinutes,
    decimal RemainingKilometers,
    int ShipmentsAhead,
    TimeSpan? AverageTimeBetweenOrders);

public sealed record DeliveryDelayRiskResult(
    decimal RiskIndex,
    decimal WindowScore,
    decimal SpeedScore,
    decimal QueueScore);

public static class DeliveryDelayRiskCalculator
{
    public const decimal WindowVariantMax = 45m;
    public const decimal SpeedVariantMax = 33m;
    public const decimal QueueVariantMax = 22m;

    public static DeliveryDelayRiskResult Evaluate(DeliveryDelayRiskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var windowScore = CalculateWindowScore(input.DeliveryWindowEndAt, input.EstimatedArrivalAt);
        var speedScore = CalculateSpeedScore(input.ArrivalMinutes, input.RemainingKilometers);
        var queueScore = CalculateQueueScore(input.ShipmentsAhead, input.AverageTimeBetweenOrders);

        return new DeliveryDelayRiskResult(
            windowScore + speedScore + queueScore,
            windowScore,
            speedScore,
            queueScore);
    }

    public static decimal CalculateWindowScore(DateTime? windowEndAt, DateTime estimatedArrivalAt)
    {
        if (windowEndAt is not { } end)
        {
            return 0m;
        }

        var remaining = (end - estimatedArrivalAt).TotalMinutes;

        if (remaining > 30)
        {
            return 0m;
        }

        if (remaining > 20)
        {
            return 15m;
        }

        if (remaining > 10)
        {
            return 30m;
        }

        return 45m;
    }

    public static decimal CalculateSpeedScore(double arrivalMinutes, decimal remainingKilometers)
    {
        if (arrivalMinutes <= 0)
        {
            return 0m;
        }

        var speedKph = (double)remainingKilometers / (arrivalMinutes / 60d);

        if (speedKph <= 60)
        {
            return 0m;
        }

        if (speedKph <= 70)
        {
            return 11m;
        }

        if (speedKph <= 80)
        {
            return 22m;
        }

        return 33m;
    }

    public static decimal CalculateQueueScore(int shipmentsAhead, TimeSpan? averageTimeBetweenOrders)
    {
        if (shipmentsAhead <= 0 || averageTimeBetweenOrders is not { } average)
        {
            return 0m;
        }

        var minutes = shipmentsAhead * average.TotalMinutes;

        if (minutes < 15)
        {
            return 0m;
        }

        if (minutes < 25)
        {
            return 7.33m;
        }

        if (minutes < 40)
        {
            return 14.66m;
        }

        return 22m;
    }
}