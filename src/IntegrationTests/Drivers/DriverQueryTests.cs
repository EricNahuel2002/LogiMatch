using Application.Dtos.Drivers;
using Domain.Entities;
using Domain.ValueObjects;
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
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        route.AssignDriver(driver);
        arrange.Db.Routes.Add(route);
        await arrange.Db.SaveChangesAsync();

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
}
