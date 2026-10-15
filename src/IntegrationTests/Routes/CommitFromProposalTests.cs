using Application.Dtos.Routes;
using Domain.Entities;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Routes;

[Collection(DatabaseCollection.Name)]
public class CommitFromProposalTests
{
    private readonly TestDatabaseFixture _fixture;

    public CommitFromProposalTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CommitFromProposal_CreatesRouteWithDriverVehicleAndStops()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);
        var vehicle = await arrange.Db.Vehicles.FirstAsync();
        var deposit = await arrange.Db.Deposits.FirstAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        var stop = await arrange.Db.RouteStops.SingleAsync(rs => rs.ShipmentId == shipment.Id);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var request = new CommitFromProposalRequest([
            new CommitProposalAssignmentRequest(
                driver.Id,
                vehicle.Id,
                deposit.Id,
                [new CommitProposalStopRequest(shipment.Id, 1)])
        ]);

        var response = await client.PostAsJsonAsync("/api/routes/commit-from-proposal", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<CommitFromProposalResponse>();
        Assert.NotNull(body);
        var committed = Assert.Single(body!.Routes);
        Assert.Equal(driver.Id, committed.DriverId);
        Assert.Equal(stop.Id, Assert.Single(committed.RouteStopIds));

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var route = await verify.Db.Routes.SingleAsync(r => r.Id == committed.RouteId);
        Assert.Equal(driver.Id, route.DriverId);
        Assert.Equal(vehicle.Id, route.VehicleId);
        Assert.Equal(deposit.Coordinate, route.Origin);

        var storedStop = await verify.Db.RouteStops.SingleAsync(rs => rs.Id == stop.Id);
        Assert.Equal(committed.RouteId, storedStop.RouteId);
    }

    [Fact]
    public async Task CommitFromProposal_MultipleStops_AppliesProposalOrder()
    {
        // Both stops are created with order 1, as the UI does before any route exists. Committing a
        // two-stop proposal must renumber them to the planned sequence instead of colliding on 1.
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);
        var vehicle = await arrange.Db.Vehicles.FirstAsync();
        var deposit = await arrange.Db.Deposits.FirstAsync();
        var first = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        var second = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 8m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var request = new CommitFromProposalRequest([
            new CommitProposalAssignmentRequest(
                driver.Id,
                vehicle.Id,
                deposit.Id,
                [
                    new CommitProposalStopRequest(second.Id, 2),
                    new CommitProposalStopRequest(first.Id, 1)
                ])
        ]);

        var response = await client.PostAsJsonAsync("/api/routes/commit-from-proposal", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<CommitFromProposalResponse>();
        Assert.NotNull(body);
        var committed = Assert.Single(body!.Routes);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var stops = await verify.Db.RouteStops
            .Where(rs => rs.RouteId == committed.RouteId)
            .OrderBy(rs => rs.StopOrder)
            .Select(rs => new { rs.ShipmentId, rs.StopOrder })
            .ToListAsync();

        Assert.Equal(2, stops.Count);
        Assert.Equal(first.Id, stops[0].ShipmentId);
        Assert.Equal(1, stops[0].StopOrder);
        Assert.Equal(second.Id, stops[1].ShipmentId);
        Assert.Equal(2, stops[1].StopOrder);
    }

    [Fact]
    public async Task CommitFromProposal_TwoRoutesSharingDeposit_PersistsBothOrigins()
    {
        // Two routes that start from the same deposit share the deposit's Coordinate instance.
        // EF Core owned types cannot track one instance for two owners, so each route must own a
        // copy; otherwise the second insert writes NULL into Origin_Latitude.
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var drivers = await arrange.Db.Users.OfType<Driver>()
            .Where(d => d.Email == "chofer1@logimatch.com" || d.Email == "chofer2@logimatch.com")
            .Include(d => d.Vehicles)
            .OrderBy(d => d.Email)
            .ToListAsync();
        var firstDriver = drivers[0];
        var secondDriver = drivers[1];
        var firstVehicle = firstDriver.Vehicles.First();
        var secondVehicle = secondDriver.Vehicles.First();
        var deposit = await arrange.Db.Deposits.FirstAsync();

        var firstShipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);
        var secondShipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 8m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var request = new CommitFromProposalRequest([
            new CommitProposalAssignmentRequest(
                firstDriver.Id,
                firstVehicle.Id,
                deposit.Id,
                [new CommitProposalStopRequest(firstShipment.Id, 1)]),
            new CommitProposalAssignmentRequest(
                secondDriver.Id,
                secondVehicle.Id,
                deposit.Id,
                [new CommitProposalStopRequest(secondShipment.Id, 1)])
        ]);

        var response = await client.PostAsJsonAsync("/api/routes/commit-from-proposal", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<CommitFromProposalResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Routes.Length);

        var routeIds = body.Routes.Select(r => r.RouteId).ToList();

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var origins = await verify.Db.Routes
            .AsNoTracking()
            .Where(r => routeIds.Contains(r.Id))
            .Select(r => r.Origin)
            .ToListAsync();

        Assert.Equal(2, origins.Count);
        Assert.All(origins, origin => Assert.Equal(deposit.Coordinate, origin));
    }

    [Fact]
    public async Task CommitFromProposal_EmptyAssignments_ReturnsBadRequest()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.PostAsJsonAsync(
            "/api/routes/commit-from-proposal",
            new CommitFromProposalRequest([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CommitFromProposal_UnknownDeposit_ReturnsNotFound()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var driver = await arrange.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);
        var vehicle = await arrange.Db.Vehicles.FirstAsync();
        var shipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Item", 10m, 1, 5m)]);

        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var request = new CommitFromProposalRequest([
            new CommitProposalAssignmentRequest(
                driver.Id,
                vehicle.Id,
                Guid.NewGuid(),
                [new CommitProposalStopRequest(shipment.Id, 1)])
        ]);

        var response = await client.PostAsJsonAsync("/api/routes/commit-from-proposal", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CommitFromProposal_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/routes/commit-from-proposal",
            new CommitFromProposalRequest([]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CommitFromProposal_WithDriverToken_ReturnsForbidden()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PostAsJsonAsync(
            "/api/routes/commit-from-proposal",
            new CommitFromProposalRequest([]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
