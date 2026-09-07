using Application.Dtos.Orders;
using Application.Dtos.Routes;
using Application.Dtos.RouteStops;
using Application.Dtos.Shipments;
using Application.Validators;

namespace UnitTests.Application;

public class ValidatorsTests
{
    [Fact]
    public void CreateOrderValidator_WhenNoItems_Fails()
    {
        var validator = new CreateOrderValidator();

        var result = validator.Validate(new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            CreatedByAdminId = Guid.NewGuid(),
            Items = []
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items");
    }

    [Fact]
    public void CreateOrderValidator_WhenCustomerIdEmpty_Fails()
    {
        var validator = new CreateOrderValidator();

        var result = validator.Validate(new CreateOrderRequest
        {
            CustomerId = Guid.Empty,
            CreatedByAdminId = Guid.NewGuid(),
            Items = [new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 1 }]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CustomerId");
    }

    [Fact]
    public void CreateOrderItemValidator_WhenQuantityNotPositive_Fails()
    {
        var validator = new CreateOrderItemValidator();

        var result = validator.Validate(new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 0, WeightKg = 1m });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void CreateOrderItemValidator_WhenWeightNotPositive_Fails()
    {
        var validator = new CreateOrderItemValidator();

        var result = validator.Validate(new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 1, WeightKg = 0 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "WeightKg");
    }

    [Fact]
    public void AssignOrderDriverValidator_WhenNullDriverId_Passes()
    {
        var validator = new AssignOrderDriverValidator();

        var result = validator.Validate(new AssignOrderDriverRequest { DriverId = null });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AssignOrderDriverValidator_WhenEmptyDriverId_Fails()
    {
        var validator = new AssignOrderDriverValidator();

        var result = validator.Validate(new AssignOrderDriverRequest { DriverId = Guid.Empty });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateRouteValidator_WhenLatitudeOutOfRange_Fails()
    {
        var validator = new CreateRouteValidator();

        var result = validator.Validate(new CreateRouteRequest { Latitude = 91m, Longitude = 0m });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Latitude");
    }

    [Fact]
    public void CreateRouteStopValidator_WhenStopOrderNotPositive_Fails()
    {
        var validator = new CreateRouteStopValidator();

        var result = validator.Validate(new CreateRouteStopRequest
        {
            ShipmentId = Guid.NewGuid(),
            Latitude = 1m,
            Longitude = 2m,
            StopOrder = 0
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "StopOrder");
    }

    [Fact]
    public void RegisterDeliveryAttemptValidator_WhenNoteTooLong_Fails()
    {
        var validator = new RegisterDeliveryAttemptValidator();

        var result = validator.Validate(new RegisterDeliveryAttemptRequest
        {
            Note = new string('a', 501)
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Note");
    }
}