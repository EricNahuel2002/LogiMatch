using Application.Dtos.Routes;
using Domain.Entities;
using Domain.ValueObjects;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Routes;

[Collection(DatabaseCollection.Name)]
public class RouteQueryTests
{
    private readonly TestDatabaseFixture _fixture;

    public RouteQueryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedRoute()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        arrange.Db.Routes.Add(route);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/routes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<RouteSummaryDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, r => r.Id == route.Id && r.IsActive);
    }

    [Fact]
    public async Task GetById_ReturnsRouteWithStops()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var route = Route.Create(new Coordinate(-34.6m, -58.4m));
        arrange.Db.Routes.Add(route);

        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        var stop = RouteStop.Create(shipment, new Coordinate(-34.6083m, -58.3816m), 1, "Sucursal");
        stop.AssignToRoute(route);
        route.AddStop(stop);
        arrange.Db.RouteStops.Add(stop);
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/routes/{route.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RouteDetailsDto>();
        Assert.NotNull(body);
        Assert.Equal(route.Id, body!.Id);
        var returnedStop = Assert.Single(body.Stops);
        Assert.Equal(stop.Id, returnedStop.Id);
        Assert.Equal(shipment.Id, returnedStop.ShipmentId);
    }

    [Fact]
    public async Task GetById_UnknownRoute_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/routes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/routes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
