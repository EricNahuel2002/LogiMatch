using Application.Dtos.Orders;
using Domain.Entities;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Orders;

[Collection(DatabaseCollection.Name)]
public class OrderLifecycleTests
{
    private readonly TestDatabaseFixture _fixture;

    public OrderLifecycleTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateOrder_CreatesOrderWithItems()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var customer = await arrange.Db.Users.OfType<Customer>().FirstAsync();
        var admin = await arrange.Db.Users.OfType<Admin>().FirstAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                CustomerId = customer.Id,
                CreatedByAdminId = admin.Id,
                Items =
                [
                    new CreateOrderItemRequest { Name = "Caja de repuestos", Price = 250m, Quantity = 2, WeightKg = 10m }
                ]
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        Assert.NotNull(body);
        Assert.Equal($"/api/orders/{body!.Id}", response.Headers.Location?.ToString());

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var order = await verify.Db.Orders
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == body.Id);

        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(admin.Id, order.CreatedByAdminId);
        Assert.Single(order.Items);
        Assert.Equal(10m, order.Items.Single().WeightKg);
    }

    [Fact]
    public async Task CreateOrder_UnknownCustomer_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var admin = await arrange.Db.Users.OfType<Admin>().FirstAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                CreatedByAdminId = admin.Id,
                Items = [new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 1, WeightKg = 1m }]
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithoutItems_ReturnsBadRequest()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var customer = await arrange.Db.Users.OfType<Customer>().FirstAsync();
        var admin = await arrange.Db.Users.OfType<Admin>().FirstAsync();

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                CustomerId = customer.Id,
                CreatedByAdminId = admin.Id,
                Items = []
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithoutToken_ReturnsUnauthorized()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var customer = await arrange.Db.Users.OfType<Customer>().FirstAsync();
        var admin = await arrange.Db.Users.OfType<Admin>().FirstAsync();

        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest
            {
                CustomerId = customer.Id,
                CreatedByAdminId = admin.Id,
                Items = [new CreateOrderItemRequest { Name = "Item", Price = 1m, Quantity = 1, WeightKg = 1m }]
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_AddsItemToOrder()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item inicial", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{order.Id}/items",
            new CreateOrderItemRequest { Name = "Item extra", Price = 20m, Quantity = 3, WeightKg = 7m });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Orders
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == order.Id);

        Assert.Equal(2, stored.Items.Count);
        Assert.Contains(stored.Items, i => i.Name == "Item extra" && i.WeightKg == 7m);
    }

    [Fact]
    public async Task AddItem_OnDispatchedOrder_ReturnsConflict()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item inicial", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{shipment.OrderId}/items",
            new CreateOrderItemRequest { Name = "Item tardío", Price = 20m, Quantity = 1, WeightKg = 1m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignDriver_AssignsDriverToOrder()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item inicial", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PatchAsJsonAsync(
            $"/api/orders/{order.Id}/driver",
            new AssignOrderDriverRequest { DriverId = driver.Id });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stored = await verify.Db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(driver.Id, stored.AssignedDriverId);
    }

    [Fact]
    public async Task AssignDriver_UnknownDriver_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item inicial", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PatchAsJsonAsync(
            $"/api/orders/{order.Id}/driver",
            new AssignOrderDriverRequest { DriverId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignDriver_UnknownOrder_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PatchAsJsonAsync(
            $"/api/orders/{Guid.NewGuid()}/driver",
            new AssignOrderDriverRequest { DriverId = null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record CreatedResponse(Guid Id);
}