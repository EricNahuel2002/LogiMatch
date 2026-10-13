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

        var body = await response.Content.ReadFromJsonAsync<CommitFromProposalResponse>();
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
