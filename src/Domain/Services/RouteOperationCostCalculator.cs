namespace Domain.Services;

public static class RouteOperationCostCalculator
{
    public static decimal CalculateEstimatedOperationCost(
        decimal salaryPerHour,
        int successfulDeliveriesToday,
        decimal kilometersPerDay,
        decimal fuelConsumption,
        decimal fuelPrice,
        decimal maintenanceCost,
        decimal tollCost)
    {
        EnsureNonNegative(salaryPerHour, nameof(salaryPerHour));
        EnsureNonNegative(successfulDeliveriesToday, nameof(successfulDeliveriesToday));
        EnsureNonNegative(kilometersPerDay, nameof(kilometersPerDay));
        EnsureNonNegative(fuelConsumption, nameof(fuelConsumption));
        EnsureNonNegative(fuelPrice, nameof(fuelPrice));
        EnsureNonNegative(maintenanceCost, nameof(maintenanceCost));
        EnsureNonNegative(tollCost, nameof(tollCost));

        var salary = salaryPerHour * successfulDeliveriesToday;
        var fuelCost = kilometersPerDay * fuelConsumption * fuelPrice;

        return salary + fuelCost + maintenanceCost + tollCost;
    }

    private static void EnsureNonNegative(decimal value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentException(
                $"{paramName} must be greater than or equal to zero.",
                paramName);
        }
    }
}