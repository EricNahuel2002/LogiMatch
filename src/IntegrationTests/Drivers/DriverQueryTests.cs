using Application.Dtos.Drivers;
using Domain.Entities;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Drivers;

[Collection(DatabaseCollection.Name)]
public class DriverQueryTests
{
    private readonly TestDatabaseFixture _fixture;

    public DriverQueryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetAll_ReturnsSeededDriver()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/drivers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<DriverSummaryDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, d => d.Id == driver.Id);
    }

    [Fact]
    public async Task GetAll_WithDriverToken_ReturnsForbidden()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.GetAsync("/api/drivers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMyShipments_ReturnsAssignedShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)],
            driver.Id);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.GetAsync("/api/drivers/me/shipments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<DriverShipmentDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, s => s.ShipmentId == shipment.Id);
    }

    [Fact]
    public async Task GetMyCurrentRoute_ReturnsRouteAssignedToDriver()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);
        var route = await TestDataBuilder.CreateActiveRouteAsync(arrange.Db, driver.Id);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.GetAsync("/api/drivers/me/routes/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DriverRouteCurrentDto>();
        Assert.NotNull(body);
        Assert.True(body!.IsActive);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Routes.SingleAsync(r => r.Id == body.RouteId);
        Assert.Equal(driver.Id, stored.DriverId);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task GetMyShipments_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/drivers/me/shipments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyCurrentRoute_ReturnsOrderedStopsWithCustomerItemsAndCurrentFlag()
    {
        const string driverEmail = "chofer5@logimatch.com";

        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == driverEmail);
        var first = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item A", 10m, 2, 5m)]);
        var second = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item B", 20m, 1, 3m)]);
        await TestDataBuilder.CreateActiveRouteAsync(
            arrange.Db,
            driver.Id,
            (first.Id, 1),
            (second.Id, 2));

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            driverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.GetAsync("/api/drivers/me/routes/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DriverRouteCurrentDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Stops.Count);

        var firstStop = body.Stops[0];
        Assert.Equal(first.Id, firstStop.ShipmentId);
        Assert.Equal(1, firstStop.StopOrder);
        Assert.True(firstStop.IsCurrent);
        Assert.Equal("Lucas Martínez", firstStop.CustomerName);
        var item = Assert.Single(firstStop.Items);
        Assert.Equal("Item A", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(10m, item.Price);

        var secondStop = body.Stops[1];
        Assert.Equal(second.Id, secondStop.ShipmentId);
        Assert.Equal(2, secondStop.StopOrder);
        Assert.False(secondStop.IsCurrent);
    }
}
