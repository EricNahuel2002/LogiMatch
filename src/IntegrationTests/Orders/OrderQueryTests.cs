using Application.Dtos.Orders;
using Domain.Entities;
using IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Orders;

[Collection(DatabaseCollection.Name)]
public class OrderQueryTests
{
    private readonly TestDatabaseFixture _fixture;

    public OrderQueryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedOrder()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 2, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<List<OrderSummaryDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, o => o.Id == order.Id);
    }

    [Fact]
    public async Task GetById_ReturnsOrderWithItems()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var order = await TestDataBuilder.CreateOrderAsync(
            arrange.Db,
            [OrderItem.Create("Item A", 10m, 2, 5m), OrderItem.Create("Item B", 4m, 1, 2m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<OrderDetailsDto>();
        Assert.NotNull(body);
        Assert.Equal(order.Id, body!.Id);
        Assert.Equal(2, body.Items.Length);
    }

    [Fact]
    public async Task GetById_UnknownOrder_ReturnsNotFound()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
