using Domain.Enums;
using Domain.Services;

namespace UnitTests.Domain;

public class RouteSequencePolicyTests
{
    [Fact]
    public void GetCurrentStopShipmentId_ReturnsLowestOrderNonTerminalStop()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var current = RouteSequencePolicy.GetCurrentStopShipmentId(
        [
            new RouteStopProgress(second, 2, ShipmentStatus.Pending),
            new RouteStopProgress(first, 1, ShipmentStatus.Pending)
        ]);

        Assert.Equal(first, current);
    }

    [Fact]
    public void GetCurrentStopShipmentId_SkipsTerminalStops()
    {
        var done = Guid.NewGuid();
        var next = Guid.NewGuid();

        var current = RouteSequencePolicy.GetCurrentStopShipmentId(
        [
            new RouteStopProgress(done, 1, ShipmentStatus.Finalized),
            new RouteStopProgress(next, 2, ShipmentStatus.Pending)
        ]);

        Assert.Equal(next, current);
    }

    [Fact]
    public void GetCurrentStopShipmentId_WhenEveryStopIsTerminal_ReturnsNull()
    {
        var current = RouteSequencePolicy.GetCurrentStopShipmentId(
        [
            new RouteStopProgress(Guid.NewGuid(), 1, ShipmentStatus.Finalized),
            new RouteStopProgress(Guid.NewGuid(), 2, ShipmentStatus.Cancelled)
        ]);

        Assert.Null(current);
    }

    [Fact]
    public void GetCurrentStopShipmentId_WhenNoStops_ReturnsNull()
    {
        Assert.Null(RouteSequencePolicy.GetCurrentStopShipmentId([]));
    }

    [Fact]
    public void GetCurrentStopShipmentId_AnInProgressStopRemainsCurrent()
    {
        var inProgress = Guid.NewGuid();
        var pending = Guid.NewGuid();

        var current = RouteSequencePolicy.GetCurrentStopShipmentId(
        [
            new RouteStopProgress(inProgress, 1, ShipmentStatus.InProgress),
            new RouteStopProgress(pending, 2, ShipmentStatus.Pending)
        ]);

        Assert.Equal(inProgress, current);
    }

    [Fact]
    public void IsCurrentStop_TrueForCurrentStop()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = RouteSequencePolicy.IsCurrentStop(
        [
            new RouteStopProgress(first, 1, ShipmentStatus.Pending),
            new RouteStopProgress(second, 2, ShipmentStatus.Pending)
        ], first);

        Assert.True(result);
    }

    [Fact]
    public void IsCurrentStop_FalseForLaterStop()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = RouteSequencePolicy.IsCurrentStop(
        [
            new RouteStopProgress(first, 1, ShipmentStatus.Pending),
            new RouteStopProgress(second, 2, ShipmentStatus.Pending)
        ], second);

        Assert.False(result);
    }

    [Theory]
    [InlineData(ShipmentStatus.Finalized, true)]
    [InlineData(ShipmentStatus.DeliveryFailed, true)]
    [InlineData(ShipmentStatus.Cancelled, true)]
    [InlineData(ShipmentStatus.Pending, false)]
    [InlineData(ShipmentStatus.InProgress, false)]
    [InlineData(ShipmentStatus.Stopped, false)]
    [InlineData(ShipmentStatus.Arrived, false)]
    public void IsTerminal_MatchesExpected(ShipmentStatus status, bool expected)
    {
        Assert.Equal(expected, RouteSequencePolicy.IsTerminal(status));
    }
}
