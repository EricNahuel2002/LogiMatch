using Domain.Entities;

namespace UnitTests.Domain;

public class VehicleTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m, "Brand", "Model");

        Assert.Equal("ABC123", vehicle.LicensePlate);
        Assert.Equal(1000m, vehicle.CapacityKg);
        Assert.Equal("Brand", vehicle.Brand);
        Assert.Equal("Model", vehicle.Model);
        Assert.True(vehicle.Active);
    }

    [Fact]
    public void Create_WithInvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentException>(() => Vehicle.Create("ABC123", 0m));
    }

    [Fact]
    public void SetActive_ChangesAvailability()
    {
        var vehicle = Vehicle.Create("ABC123", 1000m);

        vehicle.SetActive(false);
        Assert.False(vehicle.Active);

        vehicle.SetActive(true);
        Assert.True(vehicle.Active);
    }
}