using Application.Dtos.Shipments;
using Domain.Entities;
using Domain.Enums;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Shipments;

[Collection(DatabaseCollection.Name)]
public class PriorityRecalculationTests
{
    private readonly TestDatabaseFixture _fixture;

    public PriorityRecalculationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_AssignsPriorityToPendingShipments()
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

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var pending = await verify.Db.Shipments
            .Where(s => s.Status == ShipmentStatus.Pending)
            .ToListAsync();

        Assert.NotEmpty(pending);
        Assert.All(pending, s => Assert.True(
            Enum.IsDefined(typeof(ShipmentPriority), s.Priority),
            $"Unexpected priority {s.Priority} for shipment {s.Id}."));
    }

    [Fact]
    public async Task RegisterDeliveryAttempt_WhenCustomerAbsent_IncrementsAbsentDeliveriesCount()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        shipment.Start();
        shipment.MarkArrivedAtDestination();
        await arrange.Db.SaveChangesAsync();

        var before = shipment.Order.Customer.AbsentDeliveriesCount;

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            $"/api/shipments/{shipment.Id}/delivery-attempts",
            new RegisterDeliveryAttemptRequest
            {
                Succeeded = false,
                FailureReason = DeliveryFailureReason.CustomerAbsent,
                Note = "Nadie atendió"
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var storedCustomer = await verify.Db.Users.OfType<Customer>()
            .SingleAsync(c => c.Id == shipment.Order.CustomerId);

        Assert.Equal(before + 1, storedCustomer.AbsentDeliveriesCount);
    }
}