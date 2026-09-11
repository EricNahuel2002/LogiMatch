using Application.Dtos.RouteStops;
using Domain.Entities;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.RouteStops;

[Collection(DatabaseCollection.Name)]
public class CreateRouteStopTests
{
    private readonly TestDatabaseFixture _fixture;

    public CreateRouteStopTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateRouteStop_CreatesStopWithGeocodedCoordinate()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/route-stops",
            new CreateRouteStopRequest
            {
                ShipmentId = shipment.Id,
                StopOrder = 1,
                Address = "Av. Rivadavia 123",
                Name = "Sucursal central"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        Assert.NotNull(body);
        Assert.Equal($"/api/route-stops/{body!.Id}", response.Headers.Location?.ToString());

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stop = await verify.Db.RouteStops.SingleAsync(rs => rs.Id == body.Id);

        Assert.Equal(shipment.Id, stop.ShipmentId);
        Assert.Null(stop.RouteId);
        Assert.Equal(_fixture.Factory.Geocoding.Coordinate, stop.Coordinate);
        Assert.Equal(1, stop.StopOrder);
        Assert.Equal("Sucursal central", stop.Name);
    }

    [Fact]
    public async Task CreateRouteStop_UnknownShipment_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/route-stops",
            new CreateRouteStopRequest
            {
                ShipmentId = Guid.NewGuid(),
                StopOrder = 1,
                Address = "Av. Rivadavia 123"
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateRouteStop_InvalidPayload_ReturnsBadRequest()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/route-stops",
            new CreateRouteStopRequest
            {
                ShipmentId = shipment.Id,
                StopOrder = 0,
                Address = "Av. Rivadavia 123"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRouteStop_WithoutToken_ReturnsUnauthorized()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/route-stops",
            new CreateRouteStopRequest
            {
                ShipmentId = shipment.Id,
                StopOrder = 1,
                Address = "Av. Rivadavia 123"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record CreatedResponse(Guid Id);
}