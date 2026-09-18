using Domain.Entities;
using Domain.Enums;
using Domain.Services;

namespace UnitTests.Domain;

public class DriverAverageDeliveryTimeCalculatorTests
{
    private static readonly DateTime Base = new(2026, 9, 17, 10, 0, 0);

    [Fact]
    public void ComputeAverage_WithThreeValidShipments_AveragesConsecutiveIntervals()
    {
        var first = BuildShipment(Base.AddMinutes(0), Base.AddMinutes(60));
        var second = BuildShipment(Base.AddMinutes(90), Base.AddMinutes(120));
        var third = BuildShipment(Base.AddMinutes(150), Base.AddMinutes(180));

        var average = DriverAverageDeliveryTimeCalculator.ComputeAverage([first, second, third]);

        Assert.Equal(TimeSpan.FromMinutes(30), average);
    }

    [Fact]
    public void ComputeAverage_WithSingleShipment_ReturnsNull()
    {
        var only = BuildShipment(Base.AddMinutes(0), Base.AddMinutes(60));

        var average = DriverAverageDeliveryTimeCalculator.ComputeAverage([only]);

        Assert.Null(average);
    }

    [Fact]
    public void ComputeAverage_WhenFinalizationDateIsMissing_OmitsThatInterval()
    {
        var withoutFinal = BuildShipment(Base.AddMinutes(0), null);
        var second = BuildShipment(Base.AddMinutes(90), Base.AddMinutes(120));

        var average = DriverAverageDeliveryTimeCalculator.ComputeAverage([withoutFinal, second]);

        Assert.Null(average);
    }

    [Fact]
    public void ComputeAverage_WhenStartDateIsMissing_OmitsThatInterval()
    {
        var withoutStart = BuildShipment(null, Base.AddMinutes(60));
        var second = BuildShipment(Base.AddMinutes(90), Base.AddMinutes(120));

        var average = DriverAverageDeliveryTimeCalculator.ComputeAverage([withoutStart, second]);

        Assert.Null(average);
    }

    [Fact]
    public void ComputeAverage_WhenNextStartsBeforePreviousFinishes_SkipsNegativeInterval()
    {
        var first = BuildShipment(Base.AddMinutes(0), Base.AddMinutes(90));
        var overlapping = BuildShipment(Base.AddMinutes(60), Base.AddMinutes(120));

        var average = DriverAverageDeliveryTimeCalculator.ComputeAverage([first, overlapping]);

        Assert.Null(average);
    }

    [Fact]
    public void ComputeAverage_IgnoresShipmentsWithoutBothDates()
    {
        var incomplete = BuildShipment(null, null);
        var first = BuildShipment(Base.AddMinutes(0), Base.AddMinutes(60));
        var second = BuildShipment(Base.AddMinutes(90), Base.AddMinutes(120));

        var average = DriverAverageDeliveryTimeCalculator.ComputeAverage([incomplete, first, second]);

        Assert.Equal(TimeSpan.FromMinutes(30), average);
    }

    [Fact]
    public void ComputeAverage_WithEmptyCollection_ReturnsNull()
    {
        var average = DriverAverageDeliveryTimeCalculator.ComputeAverage([]);

        Assert.Null(average);
    }

    [Fact]
    public void ToTimeRange_TakesFirstInProgressAndFinalizedRegardlessOfInsertionOrder()
    {
        var shipment = BuildShipment(null, null);
        shipment.History.Add(new ShipmentHistory { Status = ShipmentStatus.Finalized, RecordedAt = Base.AddMinutes(120) });
        shipment.History.Add(new ShipmentHistory { Status = ShipmentStatus.InProgress, RecordedAt = Base.AddMinutes(60) });
        shipment.History.Add(new ShipmentHistory { Status = ShipmentStatus.Stopped, RecordedAt = Base.AddMinutes(30) });
        shipment.History.Add(new ShipmentHistory { Status = ShipmentStatus.InProgress, RecordedAt = Base.AddMinutes(0) });

        var range = DriverAverageDeliveryTimeCalculator.ToTimeRange(shipment);

        Assert.Equal(Base.AddMinutes(0), range.StartedAt);
        Assert.Equal(Base.AddMinutes(120), range.FinalizedAt);
    }

    private static Shipment BuildShipment(DateTime? startedAt, DateTime? finalizedAt)
    {
        var order = Order.Create(
            new Customer(),
            new Admin(),
            [OrderItem.Create("Item", 10m, 1, 1m)]);

        var shipment = Shipment.Create(order);

        if (startedAt is { } start)
        {
            shipment.History.Add(new ShipmentHistory
            {
                Status = ShipmentStatus.InProgress,
                RecordedAt = start
            });
        }

        if (finalizedAt is { } final)
        {
            shipment.History.Add(new ShipmentHistory
            {
                Status = ShipmentStatus.Finalized,
                RecordedAt = final
            });
        }

        return shipment;
    }
}