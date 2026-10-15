using Application.Dtos.Shipments;
using Domain.Entities;
using Domain.Enums;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Shipments;

[Collection(DatabaseCollection.Name)]
public class ShipmentQueryTests
{
    private readonly TestDatabaseFixture _fixture;

    public ShipmentQueryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/shipments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<List<ShipmentSummaryDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, s => s.Id == shipment.Id);
    }

    [Fact]
    public async Task GetById_ReturnsDetailsWithItems()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item A", 10m, 2, 5m), OrderItem.Create("Item B", 4m, 1, 2m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/shipments/{shipment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<ShipmentDetailsDto>();
        Assert.NotNull(body);
        Assert.Equal(shipment.Id, body!.Id);
        Assert.Equal(ShipmentStatus.Pending, body.Status);
        Assert.Equal(2, body.Items.Length);
    }

    [Fact]
    public async Task GetById_SerializesStatusEnumAsString()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/shipments/{shipment.Id}");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Pending\"", raw);
    }

    [Fact]
    public async Task GetById_UnknownShipment_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/shipments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_ReturnsRecordedTransitions()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/shipments/{shipment.Id}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<List<ShipmentHistoryDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, h => h.ShipmentId == shipment.Id && h.Status == ShipmentStatus.InProgress);
    }

    [Fact]
    public async Task GetDeliveryAttempts_ReturnsRegisteredAttempt()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        await arrange.Db.SaveChangesAsync();

        using (var driver = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword))
        {
            var register = await driver.PostAsJsonAsync(
                $"/api/shipments/{shipment.Id}/delivery-attempts",
                new RegisterDeliveryAttemptRequest { Succeeded = true, Note = "Entregado" });
            Assert.Equal(HttpStatusCode.NoContent, register.StatusCode);
        }

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/shipments/{shipment.Id}/delivery-attempts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<List<DeliveryAttemptDto>>();
        Assert.NotNull(body);
        var attempt = Assert.Single(body!, a => a.ShipmentId == shipment.Id);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.True(attempt.Succeeded);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/shipments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
