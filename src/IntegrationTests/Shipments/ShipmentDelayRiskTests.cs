using Application.Dtos.Shipments;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Shipments;

[Collection(DatabaseCollection.Name)]
public class ShipmentDelayRiskTests
{
    private readonly TestDatabaseFixture _fixture;

    public ShipmentDelayRiskTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetPending_PrefersRouteDriverOverAssignedDriver()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var routeDriver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == "chofer1@logimatch.com");
        var orderDriver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == "chofer2@logimatch.com");

        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)],
            orderDriver.Id);

        var shipment = Shipment.Create(order);
        shipment.SetDelayRiskPercentage(42.5m);

        var route = Route.Create(new Coordinate(-34.6082m, -58.3784m));
        route.AssignDriver(routeDriver);
        var stop = RouteStop.Create(shipment, new Coordinate(-34.6083m, -58.3816m), 1);
        stop.AssignToRoute(route);
        route.AddStop(stop);

        arrange.Db.Shipments.Add(shipment);
        arrange.Db.Routes.Add(route);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/shipments/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ShipmentDelayRiskResponse>>();
        Assert.NotNull(body);

        var result = body!.Single(s => s.ShipmentId == shipment.Id);
        Assert.Equal(ShipmentStatus.Pending, result.Status);
        Assert.Equal(shipment.Priority, result.Priority);
        Assert.Equal(42.5m, result.DelayRiskPercentage);
        Assert.NotNull(result.Driver);
        Assert.Equal(routeDriver.Id, result.Driver!.DriverId);
        Assert.Equal(routeDriver.Name, result.Driver.Name);
        Assert.Equal(routeDriver.Surname, result.Driver.Surname);
    }

    [Fact]
    public async Task GetPending_WithoutRoute_FallsBackToAssignedDriver()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == "chofer1@logimatch.com");

        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)],
            driver.Id);

        var shipment = Shipment.Create(order);
        shipment.SetDelayRiskPercentage(10m);

        arrange.Db.Shipments.Add(shipment);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/shipments/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ShipmentDelayRiskResponse>>();
        Assert.NotNull(body);

        var result = body!.Single(s => s.ShipmentId == shipment.Id);
        Assert.Equal(10m, result.DelayRiskPercentage);
        Assert.NotNull(result.Driver);
        Assert.Equal(driver.Id, result.Driver!.DriverId);
        Assert.Equal(driver.Name, result.Driver.Name);
        Assert.Equal(driver.Surname, result.Driver.Surname);
    }

    [Fact]
    public async Task GetPending_WithoutDriver_MapsNullDriver()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        var shipment = Shipment.Create(order);
        shipment.SetDelayRiskPercentage(5m);

        arrange.Db.Shipments.Add(shipment);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/shipments/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ShipmentDelayRiskResponse>>();
        Assert.NotNull(body);

        var result = body!.Single(s => s.ShipmentId == shipment.Id);
        Assert.Equal(5m, result.DelayRiskPercentage);
        Assert.Null(result.Driver);
    }

    [Fact]
    public async Task GetPending_ExcludesNonPendingShipments()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        var shipment = Shipment.Create(order);
        shipment.SetDelayRiskPercentage(25m);
        shipment.Start();

        arrange.Db.Shipments.Add(shipment);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/shipments/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ShipmentDelayRiskResponse>>();
        Assert.NotNull(body);
        Assert.DoesNotContain(body!, s => s.ShipmentId == shipment.Id);
    }

    [Fact]
    public async Task GetPending_WithDriverToken_ReturnsForbidden()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.GetAsync("/api/shipments/pending");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPending_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/shipments/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}