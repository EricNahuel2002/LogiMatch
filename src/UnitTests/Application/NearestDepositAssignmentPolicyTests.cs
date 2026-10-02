using Application.RoutePlanning;
using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Application;

public class NearestDepositAssignmentPolicyTests
{
    private static readonly Coordinate Retiro = new(-34.6082m, -58.3784m);
    private static readonly Coordinate Avellaneda = new(-34.6720m, -58.4400m);
    private static readonly Coordinate BahiaBlanca = new(-38.7170m, -62.2700m);

    private readonly NearestDepositAssignmentPolicy _policy = new();

    [Fact]
    public void Assign_NoDepositRequested_PicksTheNearest()
    {
        var driverId = Guid.NewGuid();
        var retiro = BuildDeposit(Retiro);
        var bahiaBlanca = BuildDeposit(BahiaBlanca);

        var result = _policy.Assign(
            [new DepositAssignment(driverId, new Coordinate(-34.61m, -58.39m), null)],
            [retiro, bahiaBlanca]);

        Assert.Equal(retiro.Id, result[driverId]);
    }

    [Fact]
    public void Assign_RequestingADeposit_KeepsItEvenWhenItIsTheFarthest()
    {
        var driverId = Guid.NewGuid();
        var requested = BuildDeposit(BahiaBlanca);

        var result = _policy.Assign(
            [new DepositAssignment(driverId, new Coordinate(-34.61m, -58.39m), requested.Id)],
            [BuildDeposit(Retiro), requested]);

        Assert.Equal(requested.Id, result[driverId]);
    }

    [Fact]
    public void Assign_MixedSelections_OverridesOnlyThePinnedOnes()
    {
        var pinnedDriverId = Guid.NewGuid();
        var freeDriverId = Guid.NewGuid();
        var requested = BuildDeposit(Avellaneda);

        var result = _policy.Assign(
            [
                new DepositAssignment(pinnedDriverId, new Coordinate(-34.61m, -58.39m), requested.Id),
                new DepositAssignment(freeDriverId, new Coordinate(-34.61m, -58.39m), null)
            ],
            [BuildDeposit(Retiro), requested]);

        Assert.Equal(requested.Id, result[pinnedDriverId]);
        Assert.NotEqual(requested.Id, result[freeDriverId]);
    }

    [Fact]
    public void Assign_TwoDrivers_ShareTheSameDepositWhenItIsNearestForBoth()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = _policy.Assign(
            [
                new DepositAssignment(first, new Coordinate(-34.61m, -58.39m), null),
                new DepositAssignment(second, new Coordinate(-34.62m, -58.41m), null)
            ],
            [BuildDeposit(Retiro)]);

        Assert.Equal(result[first], result[second]);
    }

    [Fact]
    public void Assign_TwoDrivers_AreSplitByProximity()
    {
        var downtownDriver = Guid.NewGuid();
        var southDriver = Guid.NewGuid();

        var result = _policy.Assign(
            [
                new DepositAssignment(downtownDriver, new Coordinate(-34.61m, -58.39m), null),
                new DepositAssignment(southDriver, new Coordinate(-34.69m, -58.53m), null)
            ],
            [BuildDeposit(Retiro), BuildDeposit(Avellaneda)]);

        Assert.NotEqual(result[downtownDriver], result[southDriver]);
    }

    [Fact]
    public void Assign_InactiveDepositIsSkipped()
    {
        var driverId = Guid.NewGuid();
        var closed = BuildDeposit(Retiro);
        closed.SetActive(false);
        var open = BuildDeposit(Avellaneda);

        var result = _policy.Assign(
            [new DepositAssignment(driverId, new Coordinate(-34.61m, -58.39m), null)],
            [closed, open]);

        Assert.Equal(open.Id, result[driverId]);
    }

    [Fact]
    public void Assign_NoActiveDeposits_Throws()
    {
        var closed = BuildDeposit(Retiro);
        closed.SetActive(false);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _policy.Assign(
                [new DepositAssignment(Guid.NewGuid(), Retiro, null)],
                [closed]));

        Assert.Contains("no active deposits", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assign_EmptyCatalogue_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _policy.Assign([new DepositAssignment(Guid.NewGuid(), Retiro, null)], []));

        Assert.Contains("no active deposits", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assign_EquallyDistantDeposits_PicksTheLowestId()
    {
        var driverId = Guid.NewGuid();
        var lowerId = new Guid("00000000-0000-0000-0000-000000000001");
        var higherId = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var origin = new Coordinate(0m, 0m);

        // Both sit one degree east or west of the origin, so the tie is real and not an
        // accident of rounding.
        var result = _policy.Assign(
            [new DepositAssignment(driverId, origin, null)],
            [
                BuildDeposit(new Coordinate(0m, 1m), higherId),
                BuildDeposit(new Coordinate(0m, -1m), lowerId)
            ]);

        Assert.Equal(lowerId, result[driverId]);
    }

    [Fact]
    public void Assign_DriversOnOppositeSidesOfTheEarth_StillResolve()
    {
        var driverId = Guid.NewGuid();
        var origin = new Coordinate(0m, 0m);
        var near = new Guid("00000000-0000-0000-0000-000000000001");
        var far = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");

        // Antipodes make the haversine argument land on 1, which has to be clamped before the
        // arcsine or the result becomes NaN.
        var result = _policy.Assign(
            [new DepositAssignment(driverId, origin, null)],
            [
                BuildDeposit(new Coordinate(0m, 179.999999m), far),
                BuildDeposit(new Coordinate(0m, 1m), near)
            ]);

        Assert.Equal(near, result[driverId]);
    }

    private static Deposit BuildDeposit(Coordinate coordinate, Guid? id = null)
    {
        var deposit = Deposit.Create("Deposit", coordinate);
        typeof(Deposit).GetProperty(nameof(Deposit.Id))!.SetValue(deposit, id ?? Guid.NewGuid());
        return deposit;
    }
}