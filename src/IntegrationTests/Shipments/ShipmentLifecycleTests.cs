using Application.Dtos.Shipments;
using Domain.Entities;
using Domain.Enums;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Shipments;

[Collection(DatabaseCollection.Name)]
public class ShipmentLifecycleTests
{
    private readonly TestDatabaseFixture _fixture;

    public ShipmentLifecycleTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_CreatesPendingShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/shipments?orderId={order.Id}",
            content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        Assert.NotNull(body);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await verify.Db.Shipments.SingleAsync(s => s.Id == body!.Id);
        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        Assert.Equal(order.Id, shipment.OrderId);
    }

    [Fact]
    public async Task Create_UnknownOrder_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsync(
            $"/api/shipments?orderId={Guid.NewGuid()}",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Start_StartsShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{shipment.Id}/start",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Shipments
            .Include(s => s.History)
            .SingleAsync(s => s.Id == shipment.Id);

        Assert.Equal(ShipmentStatus.InProgress, stored.Status);
        Assert.Contains(stored.History, h => h.Status == ShipmentStatus.InProgress);
    }

    [Fact]
    public async Task Start_AlreadyInProgress_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{shipment.Id}/start",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Start_UnknownShipment_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsync(
            $"/api/shipments/{Guid.NewGuid()}/start",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Start_WithoutToken_ReturnsUnauthorized()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/shipments/{shipment.Id}/start",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Stop_StopsInProgressShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/stop",
            new StopShipmentRequest { Note = "Pausa por tránsito" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Shipments.SingleAsync(s => s.Id == shipment.Id);
        Assert.Equal(ShipmentStatus.Stopped, stored.Status);
    }

    [Fact]
    public async Task Stop_NonInProgressShipment_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/stop",
            new StopShipmentRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Resume_ResumesStoppedShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        shipment.Stop();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/resume",
            new ResumeShipmentRequest { Note = "Continuo el viaje" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Shipments.SingleAsync(s => s.Id == shipment.Id);
        Assert.Equal(ShipmentStatus.InProgress, stored.Status);
    }

    [Fact]
    public async Task Arrived_MarksShipmentArrived()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/arrived",
            new MarkArrivedRequest());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Shipments.SingleAsync(s => s.Id == shipment.Id);
        Assert.Equal(ShipmentStatus.Arrived, stored.Status);
        Assert.True(stored.ArrivedAtDestination);
    }

    [Fact]
    public async Task Arrived_NonInProgressShipment_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/arrived",
            new MarkArrivedRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_CancelsPendingShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/cancel",
            new CancelShipmentRequest { Note = "Cliente desistió" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Shipments.SingleAsync(s => s.Id == shipment.Id);
        Assert.Equal(ShipmentStatus.Cancelled, stored.Status);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Cancel();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/cancel",
            new CancelShipmentRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithDriverToken_ReturnsForbidden()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/cancel",
            new CancelShipmentRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDeliveryAttempt_SuccessFinalizesShipment()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/delivery-attempts",
            new RegisterDeliveryAttemptRequest { Succeeded = true, Note = "Entregado al cliente" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Shipments
            .Include(s => s.DeliveryAttempts)
            .SingleAsync(s => s.Id == shipment.Id);

        Assert.Equal(ShipmentStatus.Finalized, stored.Status);
        var attempt = Assert.Single(stored.DeliveryAttempts);
        Assert.True(attempt.Succeeded);
        Assert.Equal(1, attempt.AttemptNumber);
    }

    [Fact]
    public async Task RegisterDeliveryAttempt_NotArrived_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        await arrange.Db.SaveChangesAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/delivery-attempts",
            new RegisterDeliveryAttemptRequest { Succeeded = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record CreatedResponse(Guid Id);
}