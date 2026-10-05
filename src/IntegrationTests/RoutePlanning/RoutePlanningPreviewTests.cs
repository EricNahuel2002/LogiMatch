using System.Net;
using System.Net.Http.Json;
using Application.Dtos.RoutePlanning;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests.RoutePlanning;

[Collection(DatabaseCollection.Name)]
public class RoutePlanningPreviewTests
{
    private readonly TestDatabaseFixture _fixture;

    public RoutePlanningPreviewTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly Coordinate DepositCoordinate = new(-34.5885m, -58.4299m);

    /// <summary>
    /// Creates a deposit plus a pending shipment that already has a geocoded route stop, and
    /// returns the first demo driver with its first vehicle so the preview has a real
    /// selection to work with.
    /// </summary>
    private async Task<(Guid DepositId, Guid DriverId, Guid VehicleId, Shipment Shipment)> ArrangeAsync()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();

        var deposit = Deposit.Create("Deposito central", DepositCoordinate);
        arrange.Db.Deposits.Add(deposit);

        var shipment = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Caja", 10m, 1, 5m)]);

        var driver = await arrange.Db.Users.OfType<Driver>()
            .Include(d => d.Vehicles)
            .FirstAsync(d => d.CurrentLocation != null && d.Vehicles.Any(v => v.Active));

        await arrange.Db.SaveChangesAsync();

        return (deposit.Id, driver.Id, driver.Vehicles.First(v => v.Active).Id, shipment);
    }

    /// <summary>
    /// Leaves one single active deposit, sitting exactly where the given driver is, so the
    /// automatic pick cannot be decided by a tie against deposits left behind by another
    /// scenario running against the same database.
    /// </summary>
    private async Task<Deposit> ArrangeSoleActiveDepositAtDriverAsync(Guid driverId)
    {
        using var session = _fixture.Factory.OpenDatabaseAsync();

        foreach (var other in await session.Db.Deposits.ToListAsync())
        {
            other.SetActive(false);
        }

        var driver = await session.Db.Users
            .OfType<Driver>()
            .FirstAsync(d => d.Id == driverId);

        var deposit = Deposit.Create("Deposito unico activo", driver.CurrentLocation!);
        session.Db.Deposits.Add(deposit);
        await session.Db.SaveChangesAsync();

        return deposit;
    }

    private async Task RestoreEveryDepositAsync()
    {
        using var session = _fixture.Factory.OpenDatabaseAsync();

        foreach (var deposit in await session.Db.Deposits.ToListAsync())
        {
            deposit.SetActive(true);
        }

        await session.Db.SaveChangesAsync();
    }

    private async Task<HttpClient> CreateAdminClientAsync() =>
        await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

    private static PlanRoutesRequest Request(
        Guid? depositId,
        Guid driverId,
        Guid vehicleId) => new()
    {
        DriverSelections =
        [
            new DriverSelectionRequest
            {
                DriverId = driverId,
                VehicleId = vehicleId,
                DepositId = depositId
            }
        ]
    };

    /// <summary>
    /// Creates a deposit exactly where the given driver is standing, which makes it the nearest
    /// one whatever else the catalogue holds, and hands it back for the assertions.
    /// </summary>
    private async Task<Deposit> CreateDepositAtDriverAsync(Guid driverId, bool active = true)
    {
        using var session = _fixture.Factory.OpenDatabaseAsync();

        var driver = await session.Db.Users
            .OfType<Driver>()
            .FirstAsync(d => d.Id == driverId);

        var deposit = Deposit.Create(
            "Deposito en la posicion del conductor",
            driver.CurrentLocation!);

        deposit.SetActive(active);
        session.Db.Deposits.Add(deposit);
        await session.Db.SaveChangesAsync();

        return deposit;
    }

    /// <summary>
    /// Declares which shipments are the pending ones of a scenario. The whole suite shares one
    /// database, and the preview no longer takes a shipment list, so every test has to park the
    /// pending shipments that belong to somebody else and hand them back when it is done.
    /// </summary>
    private async Task<Guid[]> ParkForeignPendingShipmentsAsync(params Guid[] keepPending)
    {
        using var session = _fixture.Factory.OpenDatabaseAsync();

        var parked = await session.Db.Shipments
            .Where(s => s.Status == ShipmentStatus.Pending && !keepPending.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        await TestDataBuilder.SetShipmentStatusAsync(session.Db, parked, ShipmentStatus.Stopped);

        return [.. parked];
    }

    private async Task RestorePendingShipmentsAsync(IReadOnlyCollection<Guid> shipmentIds)
    {
        using var session = _fixture.Factory.OpenDatabaseAsync();
        await TestDataBuilder.SetShipmentStatusAsync(session.Db, shipmentIds, ShipmentStatus.Pending);
    }

    [Fact]
    public async Task Preview_ValidSelection_ReturnsProposalWithoutPersistingAnything()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);
        _fixture.Factory.RouteMatrix.Reset();

        try
        {
            int routesBefore;
            using (var before = _fixture.Factory.OpenDatabaseAsync())
            {
                routesBefore = await before.Db.Routes.CountAsync();
            }

            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            var assignment = Assert.Single(proposal!.Assignments);
            Assert.Equal(arranged.DriverId, assignment.DriverId);
            Assert.Equal(arranged.VehicleId, assignment.VehicleId);
            Assert.Equal(arranged.DepositId, assignment.DepositId);
            Assert.Equal(5m, assignment.LoadKg);
            Assert.Equal(arranged.Shipment.Id, Assert.Single(assignment.Stops).ShipmentId);
            Assert.Equal(1, proposal.DriverCount);
            Assert.Equal(1, proposal.VehicleCount);
            Assert.Equal(1, proposal.ShipmentCount);
            Assert.Empty(proposal.ExcludedShipments);
            Assert.True(proposal.TotalDistanceMeters > 0);
            Assert.True(proposal.TotalDurationMinutes > 0);

            // The matrix was asked once, for every node the solver needed.
            Assert.Equal(1, _fixture.Factory.RouteMatrix.CallCount);
            Assert.Equal(3, _fixture.Factory.RouteMatrix.RequestedLocations.Count);

            // A proposal is read only: no route was created and the shipment stop stays unassigned.
            using var verify = _fixture.Factory.OpenDatabaseAsync();
            Assert.Equal(routesBefore, await verify.Db.Routes.CountAsync());
            Assert.Null(await verify.Db.RouteStops
                .Where(rs => rs.ShipmentId == arranged.Shipment.Id)
                .Select(rs => rs.RouteId)
                .FirstOrDefaultAsync());
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_MultiplePendingShipments_AssignsEveryShipmentExactlyOnce()
    {
        var arranged = await ArrangeAsync();

        using var extra = _fixture.Factory.OpenDatabaseAsync();
        var second = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            extra.Db,
            [OrderItem.Create("Caja", 10m, 1, 8m)]);
        var third = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            extra.Db,
            [OrderItem.Create("Caja", 10m, 1, 2m)]);

        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id, second.Id, third.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Equal(3, proposal!.ShipmentCount);
            Assert.Equal(15m, Assert.Single(proposal.Assignments).LoadKg);

            var stops = proposal.Assignments.SelectMany(a => a.Stops).Select(s => s.ShipmentId).ToList();
            Assert.Equal(3, stops.Count);
            Assert.Equal(3, stops.Distinct().Count());
            Assert.Equal(
                new[] { arranged.Shipment.Id, second.Id, third.Id }.Order(),
                stops.Order());

            var orders = proposal.Assignments.SelectMany(a => a.Stops)
                .Select(s => s.StopOrder)
                .Order()
                .ToArray();
            Assert.Equal([1, 2, 3], orders);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_NoPendingShipments_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync();

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("no pending shipments", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_UnknownDriver_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, Guid.NewGuid(), arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_UnknownDeposit_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(Guid.NewGuid(), arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_WithoutDeposit_PlannerChoosesTheNearestOne()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);
        var nearest = await ArrangeSoleActiveDepositAtDriverAsync(arranged.DriverId);
        _fixture.Factory.RouteMatrix.Reset();

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(depositId: null, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            var assignment = Assert.Single(proposal!.Assignments);
            Assert.Equal(nearest.Id, assignment.DepositId);

            // One node for the driver, one for the shipment and one for the chosen deposit:
            // the rest of the catalogue never reaches the matrix.
            Assert.Equal(3, _fixture.Factory.RouteMatrix.RequestedLocations.Count);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
            await RestoreEveryDepositAsync();
        }
    }

    [Fact]
    public async Task Preview_WithoutDeposit_SkipsTheInactiveOnes()
    {
        var arranged = await ArrangeAsync();
        var inactiveNearest = await CreateDepositAtDriverAsync(arranged.DriverId, active: false);
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(depositId: null, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            var assignment = Assert.Single(proposal!.Assignments);
            Assert.NotEqual(inactiveNearest.Id, assignment.DepositId);

            using var verify = _fixture.Factory.OpenDatabaseAsync();
            var chosen = await verify.Db.Deposits
                .FirstAsync(d => d.Id == assignment.DepositId);
            Assert.True(chosen.Active);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_RequestedInactiveDeposit_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();
        var inactive = await CreateDepositAtDriverAsync(arranged.DriverId, active: false);
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(inactive.Id, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_RequestedDepositOverridesThePlanner()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);
        var nearest = await CreateDepositAtDriverAsync(arranged.DriverId);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            var assignment = Assert.Single(proposal!.Assignments);
            Assert.Equal(arranged.DepositId, assignment.DepositId);
            Assert.NotEqual(nearest.Id, assignment.DepositId);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_PendingShipmentWithoutRouteStop_IsReportedAsExcluded()
    {
        var arranged = await ArrangeAsync();

        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var withoutStop = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Caja", 10m, 1, 3m)]);

        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id, withoutStop.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Equal(1, proposal!.ShipmentCount);
            Assert.Equal(arranged.Shipment.Id, Assert.Single(proposal.Assignments).Stops[0].ShipmentId);

            var excluded = Assert.Single(proposal.ExcludedShipments);
            Assert.Equal(withoutStop.Id, excluded.ShipmentId);
            Assert.Contains("geocoded delivery coordinate", excluded.Reason);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_PendingShipmentWithElapsedDeliveryWindow_IsReportedAsExcluded()
    {
        var arranged = await ArrangeAsync();

        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var elapsed = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Caja", 10m, 1, 4m)],
            deliveryWindowStartAt: DateTime.Now.AddHours(-6),
            deliveryWindowEndAt: DateTime.Now.AddHours(-2));

        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id, elapsed.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Equal(1, proposal!.ShipmentCount);
            Assert.Equal(arranged.Shipment.Id, Assert.Single(proposal.Assignments).Stops[0].ShipmentId);

            var excluded = Assert.Single(proposal.ExcludedShipments);
            Assert.Equal(elapsed.Id, excluded.ShipmentId);
            Assert.Contains("delivery window already closed", excluded.Reason);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_PendingShipmentWithDeliveryWindowBeyondToday_IsReportedAsExcluded()
    {
        var arranged = await ArrangeAsync();

        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var beyond = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Caja", 10m, 1, 4m)],
            deliveryWindowStartAt: DateTime.Now.AddHours(20),
            deliveryWindowEndAt: DateTime.Now.AddHours(22));

        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id, beyond.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Equal(1, proposal!.ShipmentCount);
            Assert.Equal(arranged.Shipment.Id, Assert.Single(proposal.Assignments).Stops[0].ShipmentId);

            var excluded = Assert.Single(proposal.ExcludedShipments);
            Assert.Equal(beyond.Id, excluded.ShipmentId);
            Assert.Contains("beyond today's planning horizon", excluded.Reason);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_EveryPendingShipmentExcluded_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();

        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var withoutStop = await TestDataBuilder.CreatePendingShipmentAsync(
            arrange.Db,
            [OrderItem.Create("Caja", 10m, 1, 3m)]);

        var parked = await ParkForeignPendingShipmentsAsync(withoutStop.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains(withoutStop.Id.ToString(), await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_OnlyPendingShipmentsArePlanned()
    {
        var arranged = await ArrangeAsync();

        Shipment plannable;
        using (var arrange = _fixture.Factory.OpenDatabaseAsync())
        {
            var shipment = await arrange.Db.Shipments
                .Include(s => s.Order)
                .FirstAsync(s => s.Id == arranged.Shipment.Id);
            shipment.Start();
            await arrange.Db.SaveChangesAsync();

            plannable = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
                arrange.Db,
                [OrderItem.Create("Caja", 10m, 1, 7m)]);
        }

        var parked = await ParkForeignPendingShipmentsAsync(plannable.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Equal(1, proposal!.ShipmentCount);

            var stops = proposal.Assignments.SelectMany(a => a.Stops).Select(s => s.ShipmentId).ToList();
            Assert.Equal([plannable.Id], stops);
            Assert.DoesNotContain(arranged.Shipment.Id, stops);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_InactiveVehicle_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);

        using (var arrange = _fixture.Factory.OpenDatabaseAsync())
        {
            var vehicle = await arrange.Db.Vehicles.FirstAsync(v => v.Id == arranged.VehicleId);
            vehicle.SetActive(false);
            await arrange.Db.SaveChangesAsync();
        }

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            using var restore = _fixture.Factory.OpenDatabaseAsync();
            var toRestore = await restore.Db.Vehicles.FirstAsync(v => v.Id == arranged.VehicleId);
            toRestore.SetActive(true);
            await restore.Db.SaveChangesAsync();

            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_IncompleteMatrix_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);
        _fixture.Factory.RouteMatrix.Incomplete = true;

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            _fixture.Factory.RouteMatrix.Incomplete = false;
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_MatrixUnavailable_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);
        _fixture.Factory.RouteMatrix.Unavailable = true;

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            _fixture.Factory.RouteMatrix.Unavailable = false;
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_ShipmentHeavierThanAnyVehicle_ReturnsConflict()
    {
        var arranged = await ArrangeAsync();

        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var heavy = await TestDataBuilder.CreatePendingShipmentWithStopAsync(
            arrange.Db,
            [OrderItem.Create("Palet", 10m, 1, 9_999m)]);

        var parked = await ParkForeignPendingShipmentsAsync(heavy.Id);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_WithoutToken_ReturnsUnauthorized()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);

        try
        {
            using var client = _fixture.Factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_AsDriver_ReturnsForbidden()
    {
        var arranged = await ArrangeAsync();
        var parked = await ParkForeignPendingShipmentsAsync(arranged.Shipment.Id);

        try
        {
            using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
                ApiWebApplicationFactory.DriverEmail,
                ApiWebApplicationFactory.UsersPassword);

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(arranged.DepositId, arranged.DriverId, arranged.VehicleId));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    [Fact]
    public async Task Preview_UsesDemoSeedDataWithoutAnyExtraPreparation()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();

        // The seeder is what a developer has after `dotnet run`, so the demo path has to be
        // enough on its own: seeded deposits, seeded drivers with vehicles, seeded pending
        // shipments with geocoded stops. No test fixture prepares any of it.
        var depositIds = await arrange.Db.Deposits
            .Select(d => d.Id)
            .ToListAsync();
        Assert.Contains(DemoDataSeeder.CentralDepositId, depositIds);
        Assert.Contains(DemoDataSeeder.SouthDepositId, depositIds);

        // Emails are the demo accounts the seeder creates.
        var driver1 = await LoadSeededDriverAsync(arrange.Db, "chofer1@logimatch.com", "AA123BB");
        var driver5 = await LoadSeededDriverAsync(arrange.Db, "chofer5@logimatch.com", "AI444JJ");

        // Only the shipments the seeder leaves pending take part. "Caja de repuestos" is not in
        // the list because on a developer's machine its delivery window is frozen at seed time
        // and is already out of reach; on a freshly seeded database it is planned like the rest.
        var seeded = await LoadSeededPendingShipmentsAsync(
            arrange.Db,
            "Bulto textil", "Equipo de cómputo", "Mercadería general", "Cajas de vidrio", "Mercadería suelta", "Caja de repuestos");

        // Leftovers from the rest of the suite share this database, and the preview now plans
        // every pending shipment there is, so they are parked before the demo is exercised.
        var parked = await ParkForeignPendingShipmentsAsync([.. seeded.Select(s => s.Id)]);

        try
        {
            using var client = await CreateAdminClientAsync();

            // One deposit shared by both drivers, to also cover the deduplicated deposit node.
            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                new PlanRoutesRequest
                {
                    DriverSelections =
                    [
                        new DriverSelectionRequest
                        {
                            DriverId = driver1.DriverId,
                            VehicleId = driver1.VehicleId,
                            DepositId = DemoDataSeeder.CentralDepositId
                        },
                        new DriverSelectionRequest
                        {
                            DriverId = driver5.DriverId,
                            VehicleId = driver5.VehicleId,
                            DepositId = DemoDataSeeder.CentralDepositId
                        }
                    ]
                });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Empty(proposal!.ExcludedShipments);
            Assert.Equal(seeded.Count, proposal.ShipmentCount);
            Assert.NotEmpty(proposal.Assignments);

            // Every seeded pending shipment is routed exactly once, and the load is conserved.
            var stops = proposal.Assignments.SelectMany(a => a.Stops).Select(s => s.ShipmentId).ToList();
            Assert.Equal(seeded.Count, stops.Count);
            Assert.Equal(seeded.Select(s => s.Id).Order(), stops.Order());
            Assert.Equal(seeded.Sum(s => s.WeightKg), proposal.Assignments.Sum(a => a.LoadKg));

            // Which truck takes which shipment is the solver's call, so only the per-vehicle
            // capacity is asserted.
            foreach (var assignment in proposal.Assignments)
            {
                var capacity = assignment.DriverId == driver1.DriverId
                    ? driver1.CapacityKg
                    : driver5.CapacityKg;

                Assert.True(assignment.LoadKg <= capacity);
                Assert.NotEmpty(assignment.Stops);
            }
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    /// <summary>
    /// Submits the two demo drivers in the given order. The order is not cosmetic: node order in
    /// the matrix is submission order, and the stub matrix puts the second driver next to the
    /// shipments. Submitting the far one first is what makes the bigger truck belong to the weaker
    /// scored driver, which is the situation this pair of tests reproduces.
    /// </summary>
    private static PlanRoutesRequest Request(SeededDriver first, SeededDriver second, Guid depositId) => new()
    {
        DriverSelections =
        [
            new DriverSelectionRequest
            {
                DriverId = first.DriverId,
                VehicleId = first.VehicleId,
                DepositId = depositId
            },
            new DriverSelectionRequest
            {
                DriverId = second.DriverId,
                VehicleId = second.VehicleId,
                DepositId = depositId
            }
        ]
    };

    /// <summary>
    /// Loads the two demo drivers whose vehicles differ enough to matter: AA123BB carries 1500 kg
    /// and AI444JJ carries 2000 kg, and AI444JJ costs 10 more to run, so the bigger truck is never
    /// the cheaper one.
    /// </summary>
    private static async Task<(SeededDriver Small, SeededDriver Big)> LoadSeedPairAsync(LogiMatchDbContext db) =>
        (
            await LoadSeededDriverAsync(db, "chofer1@logimatch.com", "AA123BB"),
            await LoadSeededDriverAsync(db, "chofer5@logimatch.com", "AI444JJ"));

    /// <summary>
    /// The truck that only fits the heaviest shipment sits on the driver the scoring ranks last,
    /// so dropping that driver used to leave a plan nobody could complete. The score expresses a
    /// preference; capacity is a constraint, and the plan has to stay solvable.
    /// </summary>
    [Fact]
    public async Task Preview_KeepsTheOnlyTruckThatFitsEvenWhenItsDriverScoresWorst()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var (small, big) = await LoadSeedPairAsync(arrange.Db);

        // The heaviest shipment fits both trucks, so both drivers reach the ranking, but the 2800 kg
        // total does not fit either one alone. Several partitions work, so the test does not depend
        // on the solver finding one exact split.
        var created = new List<Guid>
        {
            (await TestDataBuilder.CreatePendingShipmentWithStopAsync(
                arrange.Db, [OrderItem.Create("Palet pesado", 10m, 1, 1400m)])).Id,
            (await TestDataBuilder.CreatePendingShipmentWithStopAsync(
                arrange.Db, [OrderItem.Create("Caja grande", 10m, 1, 600m)])).Id,
            (await TestDataBuilder.CreatePendingShipmentWithStopAsync(
                arrange.Db, [OrderItem.Create("Caja mediana", 10m, 1, 500m)])).Id,
            (await TestDataBuilder.CreatePendingShipmentWithStopAsync(
                arrange.Db, [OrderItem.Create("Caja chica", 10m, 1, 300m)])).Id
        };

        var parked = await ParkForeignPendingShipmentsAsync([.. created]);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(big, small, DemoDataSeeder.CentralDepositId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Empty(proposal!.ExcludedDrivers);
            Assert.Equal(4, proposal.ShipmentCount);
            Assert.Equal(2, proposal.DriverCount);
            Assert.Equal(2800m, proposal.Assignments.Sum(a => a.LoadKg));

            // Both trucks ran, which is the point: 2800 kg does not fit on either one alone.
            Assert.Equal(2, proposal.Assignments.Count);
            foreach (var assignment in proposal.Assignments)
            {
                var capacity = assignment.DriverId == small.DriverId ? small.CapacityKg : big.CapacityKg;
                Assert.True(assignment.LoadKg <= capacity);
                Assert.NotEmpty(assignment.Stops);
            }
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    /// <summary>
    /// Same two drivers, same order, but a plan small enough that one truck covers it. Here the
    /// scoring is free to have its way, which proves the previous test passed because the driver
    /// was promoted and not because it simply scored well.
    /// </summary>
    [Fact]
    public async Task Preview_DropsTheWorstScoringDriverWhenThePlanDoesNotNeedTheBigTruck()
    {
        using var arrange = _fixture.Factory.OpenDatabaseAsync();
        var (small, big) = await LoadSeedPairAsync(arrange.Db);

        var created = new List<Guid>
        {
            (await TestDataBuilder.CreatePendingShipmentWithStopAsync(
                arrange.Db, [OrderItem.Create("Caja mediana", 10m, 1, 600m)])).Id,
            (await TestDataBuilder.CreatePendingShipmentWithStopAsync(
                arrange.Db, [OrderItem.Create("Caja chica", 10m, 1, 400m)])).Id
        };

        var parked = await ParkForeignPendingShipmentsAsync([.. created]);

        try
        {
            using var client = await CreateAdminClientAsync();

            var response = await client.PostAsJsonAsync(
                "/api/route-planning/preview",
                Request(big, small, DemoDataSeeder.CentralDepositId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var proposal = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            Assert.NotNull(proposal);
            Assert.Equal(1000m, proposal!.Assignments.Sum(a => a.LoadKg));
            Assert.Equal(1, proposal.DriverCount);

            var excluded = Assert.Single(proposal.ExcludedDrivers);
            Assert.Equal(big.DriverId, excluded.DriverId);
            Assert.Equal(DriverIneligibilityReason.ScoreBelowMinimum, excluded.Reason);
            Assert.NotNull(excluded.Score);
        }
        finally
        {
            await RestorePendingShipmentsAsync(parked);
        }
    }

    private static async Task<SeededDriver> LoadSeededDriverAsync(
        LogiMatchDbContext db,
        string email,
        string licensePlate)
    {
        var driver = await db.Users.OfType<Driver>()
            .Include(d => d.Vehicles)
            .SingleAsync(d => d.Email == email);

        var vehicle = driver.Vehicles.Single(v => v.LicensePlate == licensePlate);

        Assert.NotNull(driver.CurrentLocation);
        return new SeededDriver(driver.Id, vehicle.Id, vehicle.CapacityKg);
    }

    private static async Task<IReadOnlyList<SeededShipment>> LoadSeededPendingShipmentsAsync(
        LogiMatchDbContext db,
        params string[] itemNames)
    {
        var shipments = await db.Shipments
            .Where(s => s.Status == ShipmentStatus.Pending
                && s.Order.Items.Any(i => itemNames.Contains(i.Name)))
            .Select(s => new SeededShipment(
                s.Id,
                s.Order.Items.Sum(i => i.WeightKg * i.Quantity)))
            .ToListAsync();

        Assert.Equal(itemNames.Length, shipments.Count);
        return shipments;
    }

    private sealed record SeededDriver(Guid DriverId, Guid VehicleId, decimal CapacityKg);

    private sealed record SeededShipment(Guid Id, decimal WeightKg);

    private sealed record ProposalResponse(
        IReadOnlyList<AssignmentResponse> Assignments,
        int DriverCount,
        int VehicleCount,
        int ShipmentCount,
        long TotalDistanceMeters,
        int TotalDurationMinutes,
        IReadOnlyList<ExcludedShipmentResponse> ExcludedShipments,
        IReadOnlyList<ExcludedDriverResponse> ExcludedDrivers);

    private sealed record AssignmentResponse(
        Guid DriverId,
        Guid VehicleId,
        Guid DepositId,
        decimal LoadKg,
        IReadOnlyList<StopResponse> Stops);

    private sealed record StopResponse(Guid ShipmentId, int StopOrder);

    private sealed record ExcludedShipmentResponse(Guid ShipmentId, string Reason);

    private sealed record ExcludedDriverResponse(
        Guid DriverId,
        decimal? Score,
        DriverIneligibilityReason Reason,
        string Detail);
}
