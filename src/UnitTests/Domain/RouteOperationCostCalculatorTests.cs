using Domain.Services;

namespace UnitTests.Domain;

public class RouteOperationCostCalculatorTests
{
    [Fact]
    public void Calculate_CombinesSalaryFuelMaintenanceAndToll()
    {
        var cost = RouteOperationCostCalculator.CalculateEstimatedOperationCost(
            salaryPerHour: 1000m,
            successfulDeliveriesToday: 2,
            kilometersPerDay: 50m,
            fuelConsumption: 0.12m,
            fuelPrice: 1100m,
            maintenanceCost: 80m,
            tollCost: 50m);

        Assert.Equal(8730m, cost);
    }

    [Fact]
    public void Calculate_WithoutDeliveriesOrKilometers_ReturnsFixedCosts()
    {
        var cost = RouteOperationCostCalculator.CalculateEstimatedOperationCost(
            salaryPerHour: 1000m,
            successfulDeliveriesToday: 0,
            kilometersPerDay: 0m,
            fuelConsumption: 0m,
            fuelPrice: 0m,
            maintenanceCost: 80m,
            tollCost: 50m);

        Assert.Equal(130m, cost);
    }

    [Fact]
    public void Calculate_WithNegativeSalary_Throws()
    {
        Assert.Throws<ArgumentException>(() => RouteOperationCostCalculator.CalculateEstimatedOperationCost(
            salaryPerHour: -1m,
            successfulDeliveriesToday: 0,
            kilometersPerDay: 0m,
            fuelConsumption: 0m,
            fuelPrice: 0m,
            maintenanceCost: 0m,
            tollCost: 0m));
    }

    [Fact]
    public void Calculate_WithNegativeFuelPrice_Throws()
    {
        Assert.Throws<ArgumentException>(() => RouteOperationCostCalculator.CalculateEstimatedOperationCost(
            salaryPerHour: 0m,
            successfulDeliveriesToday: 0,
            kilometersPerDay: 0m,
            fuelConsumption: 0.1m,
            fuelPrice: -1m,
            maintenanceCost: 0m,
            tollCost: 0m));
    }
}