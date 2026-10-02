using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class DepositTests
{
    private static readonly Coordinate BuenosAires = new(-34.5885m, -58.4299m);

    [Fact]
    public void Create_SetsDefaults()
    {
        var deposit = Deposit.Create("Deposito central", BuenosAires);

        Assert.Equal("Deposito central", deposit.Address);
        Assert.Equal(BuenosAires, deposit.Coordinate);
        Assert.True(deposit.Active);
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        Assert.Throws<ArgumentException>(() => Deposit.Create(" ", BuenosAires));
    }

    [Fact]
    public void Create_WithOutOfRangeCoordinate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Deposit.Create("Deposito", new Coordinate(95m, 0m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Deposit.Create("Deposito", new Coordinate(0m, 200m)));
    }

    [Fact]
    public void SetActive_TogglesTheFlag()
    {
        var deposit = Deposit.Create("Deposito central", BuenosAires);

        deposit.SetActive(false);
        Assert.False(deposit.Active);

        deposit.SetActive(true);
        Assert.True(deposit.Active);
    }
}