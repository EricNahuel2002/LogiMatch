using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class DriverTests
{
    [Fact]
    public void SetCurrentLocation_SetsLocation()
    {
        var driver = new Driver();
        var location = new Coordinate(-34.6m, -58.4m);

        driver.SetCurrentLocation(location);

        Assert.Equal(location, driver.CurrentLocation);
    }
}