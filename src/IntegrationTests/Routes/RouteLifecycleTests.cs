using Application.Dtos.Routes;
using Domain.Entities;
using Domain.ValueObjects;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Routes;

[Collection(DatabaseCollection.Name)]
public class RouteLifecycleTests
{
    private readonly TestDatabaseFixture _fixture;

    public RouteLifecycleTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateRoute_CreatesRoute()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/routes",
            new CreateRouteRequest { Latitude = -34.6m, Longitude = -58.4m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        Assert.NotNull(body);
        Assert.Equal($"/api/routes/{body!.Id}", response.Headers.Location?.ToString());

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var route = await verify.Db.Routes.SingleAsync(r => r.Id == body.Id);
        Assert.Equal(new Coordinate(-34.6m, -58.4m), route.Origin);
        Assert.True(route.IsActive);
    }

    [Fact]
    public async Task CreateRoute_InvalidLatitude_ReturnsBadRequest()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/routes",
            new CreateRouteRequest { Latitude = 100m, Longitude = -58.4m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRoute_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/routes",
            new CreateRouteRequest { Latitude = -34.6m, Longitude = -58.4m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AssignDriver_AssignsDriverToRoute()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        arrange.Db.Routes.Add(route);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PatchAsJsonAsync(
            $"/api/routes/{route.Id}/driver",
            new AssignRouteDriverRequest { DriverId = driver.Id });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(driver.Id, stored.DriverId);
    }

    [Fact]
    public async Task AssignDriver_UnknownRoute_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PatchAsJsonAsync(
            $"/api/routes/{Guid.NewGuid()}/driver",
            new AssignRouteDriverRequest { DriverId = driver.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignVehicle_AssignsVehicleToRoute()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var vehicle = await arrange.Db.Vehicles.FirstAsync();
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        arrange.Db.Routes.Add(route);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PatchAsJsonAsync(
            $"/api/routes/{route.Id}/vehicle",
            new AssignRouteVehicleRequest { VehicleId = vehicle.Id });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(vehicle.Id, stored.VehicleId);
    }

    [Fact]
    public async Task AssignVehicle_UnknownVehicle_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        arrange.Db.Routes.Add(route);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PatchAsJsonAsync(
            $"/api/routes/{route.Id}/vehicle",
            new AssignRouteVehicleRequest { VehicleId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddStop_AddsRouteStopToRoute()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        arrange.Db.Routes.Add(route);

        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        var stop = RouteStop.Create(shipment, new Coordinate(-34.6083m, -58.3816m), 1, "Sucursal");
        arrange.Db.RouteStops.Add(stop);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/routes/{route.Id}/stops/{stop.Id}",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.RouteStops.SingleAsync(rs => rs.Id == stop.Id);
        Assert.Equal(route.Id, stored.RouteId);
    }

    [Fact]
    public async Task AddStop_UnknownStop_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        arrange.Db.Routes.Add(route);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/routes/{route.Id}/stops/{Guid.NewGuid()}",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record CreatedResponse(Guid Id);
}