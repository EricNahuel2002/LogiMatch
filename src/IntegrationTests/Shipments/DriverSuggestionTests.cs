using Application.Dtos.Shipments;
using Domain.Entities;
using IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Shipments;

[Collection(DatabaseCollection.Name)]
public class DriverSuggestionTests
{
    private readonly TestDatabaseFixture _fixture;

    public DriverSuggestionTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SuggestDriver_ForFeasibleShipment_ReturnsRanking()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Paquete liviano", 50m, 1, 10m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{shipment.Id}/driver-suggestion",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DriverAssignmentSuggestion>();
        Assert.NotNull(body);
        Assert.Equal(shipment.Id, body!.ShipmentId);
        Assert.NotEmpty(body.Ranking);
        Assert.Equal(body.Ranking[0].DriverId, body.RecommendedDriverId);
    }

    [Fact]
    public async Task SuggestDriver_TooHeavyShipment_NoEligibleDrivers_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Carga pesada", 100m, 1, 2500m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{shipment.Id}/driver-suggestion",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SuggestDriver_ForNonPendingShipment_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Paquete liviano", 50m, 1, 10m)]);
        shipment.Start();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{shipment.Id}/driver-suggestion",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SuggestDriver_ForShipmentWithoutRouteStop_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Paquete liviano", 50m, 1, 10m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{shipment.Id}/driver-suggestion",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SuggestDriver_UnknownShipment_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{Guid.NewGuid()}/driver-suggestion",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}