using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Drivers;

[Collection(DatabaseCollection.Name)]
public class CancelRouteTests
{
    private readonly TestDatabaseFixture _fixture;

    public CancelRouteTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private enum ShipmentKind
    {
        Pending,
        InProgress,
        Stopped,
        Cancelled,
        Finalized
    }

    [Fact]
    public async Task Cancel_ActiveRouteRequeuesNonTerminalShipments_DeactivatesRouteAndNotifiesAdmins()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var scenario = await CreateRouteAsync(
            arrange.Db,
            ApiWebApplicationFactory.DriverEmail,
            ShipmentKind.InProgress,
            ShipmentKind.Stopped,
            ShipmentKind.Pending,
            ShipmentKind.Finalized);

        var inProgress = scenario.Shipments[0];
        var stopped = scenario.Shipments[1];
        var pending = scenario.Shipments[2];
        var finalized = scenario.Shipments[3];

        _fixture.Factory.EmailRecorder.Reset();
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            "/api/drivers/me/routes/cancel",
            new { reason = (int)RouteCancellationReason.VehicleBreakdown, note = "Engine failure" });

        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            System.IO.File.WriteAllText(
                @"C:\Users\PC\AppData\Local\Temp\opencode\cancel_debug.txt",
                await response.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var route = await verify.Db.Routes.SingleAsync(r => r.Id == scenario.Route.Id);
        Assert.False(route.IsActive);
        Assert.Equal(RouteCancellationReason.VehicleBreakdown, route.CancellationReason);

        await AssertShipmentStateAsync(verify.Db, inProgress.Shipment.Id, ShipmentStatus.Pending, null);
        await AssertShipmentStateAsync(verify.Db, stopped.Shipment.Id, ShipmentStatus.Pending, null);
        await AssertShipmentStateAsync(verify.Db, pending.Shipment.Id, ShipmentStatus.Pending, null);
        await AssertShipmentStateAsync(verify.Db, finalized.Shipment.Id, ShipmentStatus.Finalized, scenario.Driver.Id);

        Assert.False(
            await verify.Db.RouteStops.AnyAsync(rs => rs.RouteId == scenario.Route.Id));

        var email = Assert.Single(_fixture.Factory.EmailRecorder.Sent);
        Assert.Equal(new[] { "admin@logimatch.com" }, email.Recipients);
        Assert.Equal("LogiMatch - Envíos pendientes de reasignación", email.Subject);
        Assert.Contains("VehicleBreakdown", email.Body);
        Assert.Contains(inProgress.Shipment.Id.ToString(), email.Body);
        Assert.Contains(stopped.Shipment.Id.ToString(), email.Body);
        Assert.Contains(pending.Shipment.Id.ToString(), email.Body);
        Assert.DoesNotContain(finalized.Shipment.Id.ToString(), email.Body);
    }

    [Fact]
    public async Task Cancel_WithoutActiveRoute_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == "chofer2@logimatch.com");

        _fixture.Factory.EmailRecorder.Reset();
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            driver.Email!,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            "/api/drivers/me/routes/cancel",
            new { reason = (int)RouteCancellationReason.RouteAbandonment });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "application/json",
            response.Content.Headers.ContentType!.MediaType!);
        Assert.Empty(_fixture.Factory.EmailRecorder.Sent);
    }

    [Fact]
    public async Task Cancel_AlreadyDeactivatedRoute_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var scenario = await CreateRouteAsync(
            arrange.Db,
            "chofer3@logimatch.com",
            ShipmentKind.Pending);

        scenario.Route.Deactivate(RouteCancellationReason.Emergency);
        await arrange.Db.SaveChangesAsync();

        _fixture.Factory.EmailRecorder.Reset();
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            "chofer3@logimatch.com",
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            "/api/drivers/me/routes/cancel",
            new { reason = (int)RouteCancellationReason.Accident });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_fixture.Factory.EmailRecorder.Sent);
    }

    [Fact]
    public async Task Cancel_InvalidReason_ReturnsBadRequest()
    {
        _fixture.Factory.EmailRecorder.Reset();
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync("/api/drivers/me/routes/cancel", new { reason = 999 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_fixture.Factory.EmailRecorder.Sent);
    }

    [Fact]
    public async Task Cancel_TooLongNote_ReturnsBadRequest()
    {
        _fixture.Factory.EmailRecorder.Reset();
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            "/api/drivers/me/routes/cancel",
            new { reason = (int)RouteCancellationReason.VehicleBreakdown, note = new string('a', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_fixture.Factory.EmailRecorder.Sent);
    }

    [Fact]
    public async Task Cancel_AllTerminalShipments_DeactivatesRouteWithoutEmail()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var scenario = await CreateRouteAsync(
            arrange.Db,
            "chofer4@logimatch.com",
            ShipmentKind.Finalized,
            ShipmentKind.Cancelled);

        _fixture.Factory.EmailRecorder.Reset();
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            "chofer4@logimatch.com",
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            "/api/drivers/me/routes/cancel",
            new { reason = (int)RouteCancellationReason.Accident });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var route = await verify.Db.Routes.SingleAsync(r => r.Id == scenario.Route.Id);
        Assert.False(route.IsActive);
        Assert.Equal(RouteCancellationReason.Accident, route.CancellationReason);
        Assert.Equal(ShipmentStatus.Finalized, (await verify.Db.Shipments.FindAsync(scenario.Shipments[0].Shipment.Id))!.Status);
        Assert.Equal(ShipmentStatus.Cancelled, (await verify.Db.Shipments.FindAsync(scenario.Shipments[1].Shipment.Id))!.Status);
        Assert.Empty(_fixture.Factory.EmailRecorder.Sent);
    }

    [Fact]
    public async Task Cancel_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/drivers/me/routes/cancel",
            new { reason = (int)RouteCancellationReason.VehicleBreakdown });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_fixture.Factory.EmailRecorder.Sent);
    }

    [Fact]
    public async Task Cancel_WithAdminToken_ReturnsForbidden()
    {
        _fixture.Factory.EmailRecorder.Reset();
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/drivers/me/routes/cancel",
            new { reason = (int)RouteCancellationReason.RouteAbandonment });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_fixture.Factory.EmailRecorder.Sent);
    }

    private async Task<RouteScenario> CreateRouteAsync(
        LogiMatchDbContext db,
        string driverEmail,
        params ShipmentKind[] kinds)
    {
        var driver = await db.Users.OfType<Driver>().SingleAsync(d => d.Email == driverEmail);
        var customer = await db.Users.OfType<Customer>().FirstAsync();
        var admin = await db.Users.OfType<Admin>().FirstAsync();

        var route = Route.Create(new Coordinate(-34.6082m, -58.3784m));
        route.AssignDriver(driver);

        var shipments = new List<(Shipment Shipment, Order Order)>();
        var stopOrder = 1;

        foreach (var kind in kinds)
        {
            var order = Order.Create(
                customer,
                admin,
                [OrderItem.Create($"Item {driver.Email} {stopOrder}", 10m, 1, 5m)]);
            order.SetAssignedDriver(driver.Id);

            var shipment = Shipment.Create(order);
            ApplyState(shipment, kind);

            var stop = RouteStop.Create(
                shipment,
                new Coordinate(-34.6082m - stopOrder * 0.001m, -58.3784m - stopOrder * 0.001m),
                stopOrder);
            stop.AssignToRoute(route);
            route.AddStop(stop);

            shipments.Add((shipment, order));
            stopOrder++;
        }

        db.Routes.Add(route);
        await db.SaveChangesAsync();

        return new RouteScenario(route, driver, shipments);
    }

    private static void ApplyState(Shipment shipment, ShipmentKind kind)
    {
        switch (kind)
        {
            case ShipmentKind.InProgress:
                shipment.Start();
                break;

            case ShipmentKind.Stopped:
                shipment.Start();
                shipment.Stop();
                break;

            case ShipmentKind.Cancelled:
                shipment.Cancel();
                break;

            case ShipmentKind.Finalized:
                shipment.Start();
                shipment.MarkArrivedAtDestination();
                shipment.RegisterDeliveryAttempt(null, true);
                break;

            case ShipmentKind.Pending:
                break;
        }
    }

    private static async Task AssertShipmentStateAsync(
        LogiMatchDbContext db,
        Guid shipmentId,
        ShipmentStatus status,
        Guid? assignedDriverId)
    {
        var shipment = await db.Shipments
            .Include(s => s.Order)
            .SingleAsync(s => s.Id == shipmentId);

        Assert.Equal(status, shipment.Status);
        Assert.Equal(assignedDriverId, shipment.Order.AssignedDriverId);
    }

    private sealed record RouteScenario(
        Route Route,
        Driver Driver,
        IReadOnlyList<(Shipment Shipment, Order Order)> Shipments);
}