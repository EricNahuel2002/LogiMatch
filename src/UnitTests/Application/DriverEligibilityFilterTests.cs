using Application.Persistence;
using Application.RoutePlanning;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;
using Moq;

namespace UnitTests.Application;

public class DriverEligibilityFilterTests
{
    private static readonly DateTime DayStart = new(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DayEnd = new(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SelectAsync_AsksOnlyForTheSubmittedDriversAndThePlanDay()
    {
        var driverIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var pool = Pool(driverIds);
        IReadOnlyCollection<Guid>? requestedIds = null;
        var users = CandidateRepository(pool.Candidates);

        users
            .Setup(u => u.GetDriverCandidatesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, DateTime, DateTime, CancellationToken>(
                (ids, start, end, _) =>
                {
                    requestedIds = ids;
                    Assert.Equal(DayStart, start);
                    Assert.Equal(DayEnd, end);
                })
            .ReturnsAsync(pool.Candidates);

        await Filter(users).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), Matrix(3), Matrix(3), DayStart, DayEnd);

        // Scoring is relative to this plan, so no candidate outside it can be fetched: doing so
        // would change every driver's score.
        Assert.Equal(driverIds, requestedIds);
    }

    [Fact]
    public async Task SelectAsync_WithNoCandidates_KeepsEveryDriver()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);
        var users = CandidateRepository([]);

        var result = await Filter(users).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), Matrix(2), Matrix(2), DayStart, DayEnd);

        Assert.True(result.HasAnyDriver);
        Assert.Equal(driverIds, result.KeptDrivers.Select(d => d.DriverId));
        Assert.Empty(result.ExcludedDrivers);
    }

    [Fact]
    public async Task SelectAsync_KeepsTheSubmittedDriverOrder()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);
        var users = CandidateRepository(pool.Candidates);

        var result = await Filter(users).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), Matrix(3), Matrix(3), DayStart, DayEnd);

        // Driver order is driver node order in the routing problem, so ranking must not reorder it.
        Assert.Equal(driverIds, result.KeptDrivers.Select(d => d.DriverId));
    }

    [Fact]
    public async Task SelectAsync_WritesTheScoreBackIntoEveryKeptDriver()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);
        var users = CandidateRepository(pool.Candidates);

        var result = await Filter(users).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), Matrix(2), Matrix(2), DayStart, DayEnd);

        Assert.All(result.KeptDrivers, d => Assert.True(d.Score > 0m));
        Assert.All(
            result.KeptDrivers,
            d => Assert.Equal(result.ScoreByDriverId[d.DriverId], d.Score));
    }

    [Fact]
    public async Task SelectAsync_ExcludesDriversThatCannotCarryTheHeaviestShipment()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds, capacitiesKg: [500m, 5m, 500m]);

        var result = await Filter(CandidateRepository(pool.Candidates)).SelectAsync(
            pool.Drivers,
            pool.VehiclesById,
            Shipments(weightKg: 20m),
            Matrix(3),
            Matrix(3),
            DayStart,
            DayEnd);

        var excluded = Assert.Single(result.ExcludedDrivers);
        Assert.Equal(driverIds[1], excluded.DriverId);
        Assert.Equal(DriverIneligibilityReason.InsufficientVehicleCapacity, excluded.Reason);
        Assert.Null(excluded.Score);
        Assert.Contains("20", excluded.Detail);

        // A driver rejected for capacity never reaches the ranking, so it must not be normalized
        // against and it must not hold up the scores of the drivers that can work.
        Assert.DoesNotContain(driverIds[1], result.ScoreByDriverId.Keys);
        Assert.All(result.KeptDrivers, d => Assert.True(d.Score > 0m));
    }

    [Fact]
    public async Task SelectAsync_WithNoShipments_ExcludesNoDriverForCapacity()
    {
        Guid[] driverIds = [Guid.NewGuid()];
        var pool = Pool(driverIds, capacitiesKg: [1m]);

        var result = await Filter(CandidateRepository(pool.Candidates)).SelectAsync(
            pool.Drivers, pool.VehiclesById, [], Matrix(1), Matrix(1), DayStart, DayEnd);

        Assert.Empty(result.ExcludedDrivers);
        Assert.Single(result.KeptDrivers);
    }

    [Fact]
    public async Task SelectAsync_WhenNoDriverCanCarryTheHeaviestShipment_KeepsNobody()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds, capacitiesKg: [5m, 5m]);

        var result = await Filter(CandidateRepository(pool.Candidates)).SelectAsync(
            pool.Drivers,
            pool.VehiclesById,
            Shipments(weightKg: 20m),
            Matrix(2),
            Matrix(2),
            DayStart,
            DayEnd);

        // The minimum-driver backfill must not resurrect a driver with no capacity for the work.
        Assert.False(result.HasAnyDriver);
        Assert.Empty(result.KeptDrivers);
        Assert.Empty(result.ScoreByDriverId);
        Assert.Equal(2, result.ExcludedDrivers.Count);
        Assert.All(result.ExcludedDrivers, e => Assert.Null(e.Score));
    }

    [Fact]
    public async Task SelectAsync_ExcludesTheWeakestDriverBelowTheFloor()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);
        var users = CandidateRepository([
            Candidate(driverIds[0], successes: 0, pending: 5, inProgress: 5),
            Candidate(driverIds[1], successes: 10, pending: 0, inProgress: 0)
        ]);

        // Distance comes from the matrix: node 1 is the close one, so the weak driver has to sit
        // at node 0 to lose on that criterion too.
        var matrix = RouteMatrix.Create(
            3,
            [
                0, 300, 500,
                200, 0, 400,
                700, 600, 0
            ]);

        var result = await Filter(users, new DriverScoringOptions { MinimumDrivers = 1 }).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), matrix, matrix, DayStart, DayEnd);

        var excluded = Assert.Single(result.ExcludedDrivers);
        Assert.Equal(driverIds[0], excluded.DriverId);
        Assert.Equal(DriverIneligibilityReason.ScoreBelowMinimum, excluded.Reason);

        // Rejected for scoring, not for capacity, so the score is reported back for the UI.
        Assert.NotNull(excluded.Score);
        Assert.Equal(excluded.Score, result.ScoreByDriverId[driverIds[0]]);
        Assert.Equal([driverIds[1]], result.KeptDrivers.Select(d => d.DriverId));
    }

    [Fact]
    public async Task SelectAsync_ToleranceExcludesAMidRangeDriver()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);
        var users = CandidateRepository([
            Candidate(driverIds[0], successes: 10, pending: 0, inProgress: 0),
            Candidate(driverIds[1], successes: 5, pending: 2, inProgress: 2)
        ]);

        // Equal distance on purpose, so the score spread comes from the delivered count alone.
        var matrix = RouteMatrix.Create(
            3,
            [
                0, 100, 200,
                100, 0, 200,
                500, 500, 0
            ]);

        var options = new DriverScoringOptions { MinimumScore = 0m, ScoreTolerance = 0.1m };

        var result = await Filter(users, options).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), matrix, matrix, DayStart, DayEnd);

        // Above the floor but too far behind the best, which is the case the tolerance covers.
        var excluded = Assert.Single(result.ExcludedDrivers);
        Assert.Equal(driverIds[1], excluded.DriverId);
        Assert.Equal(DriverIneligibilityReason.ScoreBelowTolerance, excluded.Reason);
    }

    [Fact]
    public async Task SelectAsync_BackfillsTheBestRejectedDriverToReachTheMinimum()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);
        var users = CandidateRepository([
            Candidate(driverIds[0], successes: 10, pending: 0, inProgress: 0),
            Candidate(driverIds[1], successes: 5, pending: 2, inProgress: 2),
            Candidate(driverIds[2], successes: 0, pending: 5, inProgress: 5)
        ]);

        var matrix = RouteMatrix.Create(
            4,
            [
                0, 100, 200, 300,
                100, 0, 200, 300,
                200, 200, 0, 400,
                300, 300, 400, 0
            ]);

        // A floor of 1 drops everyone, and the tolerance of 0 does not come into it.
        var options = new DriverScoringOptions
        {
            MinimumScore = 1m,
            ScoreTolerance = 0m,
            MinimumDrivers = 2
        };

        var result = await Filter(users, options).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), matrix, matrix, DayStart, DayEnd);

        // A preview with no drivers is not a preview, so the best drivers come back regardless of
        // how bad they look.
        Assert.Equal([driverIds[0], driverIds[1]], result.KeptDrivers.Select(d => d.DriverId));
        Assert.Equal([driverIds[2]], result.ExcludedDrivers.Select(e => e.DriverId));
        Assert.All(result.KeptDrivers, d => Assert.Equal(result.ScoreByDriverId[d.DriverId], d.Score));
    }

    /// <summary>
    /// The floor can exclude every driver of a weak pool, since the score is a normalization over
    /// that pool and says nothing about a driver outside it. The best one still has to come back.
    /// </summary>
    [Fact]
    public async Task SelectAsync_AlwaysKeepsTheBestDriverWhenTheFloorDropsEveryone()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);
        var users = CandidateRepository([
            Candidate(driverIds[0], successes: 5, pending: 2, inProgress: 2),
            Candidate(driverIds[1], successes: 2, pending: 4, inProgress: 4)
        ]);

        var options = new DriverScoringOptions { MinimumScore = 1m, ScoreTolerance = 0m };

        var result = await Filter(users, options).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), Matrix(2), Matrix(2), DayStart, DayEnd);

        // A preview with no drivers is not a preview.
        var kept = Assert.Single(result.KeptDrivers);
        Assert.Equal(result.ScoreByDriverId[driverIds[0]], kept.Score);
        Assert.Equal([driverIds[1]], result.ExcludedDrivers.Select(e => e.DriverId));
    }

    /// <summary>
    /// The thresholds express a preference, so they must not shrink the fleet below what can carry
    /// the plan. This is the case a real preview hit: the better scored driver alone could not
    /// carry the weight, and dropping the other one turned a solvable plan into an infeasible one.
    /// </summary>
    [Fact]
    public async Task SelectAsync_PromotesTheScoreRejectedDriverWhenTheKeptFleetIsTooSmall()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds, capacitiesKg: [1500m, 2000m]);
        var users = CandidateRepository([
            Candidate(driverIds[0], successes: 0, pending: 5, inProgress: 5),
            Candidate(driverIds[1], successes: 10, pending: 0, inProgress: 0)
        ]);

        var result = await Filter(users).SelectAsync(
            pool.Drivers,
            pool.VehiclesById,
            ShipmentsOf(1400m, 1200m, 840m),
            SpreadMatrix().Distances,
            SpreadMatrix().Durations,
            DayStart,
            DayEnd);

        // 1500 + 2000 covers the 3440 kg, so the weak driver comes back with its score intact.
        Assert.Equal([driverIds[0], driverIds[1]], result.KeptDrivers.Select(d => d.DriverId));
        Assert.Empty(result.ExcludedDrivers);
        Assert.All(result.KeptDrivers, d => Assert.True(d.Score > 0m));

        // Proof the driver is in the plan because of the weight rather than because it scored well:
        // on its own it lands below the floor the thresholds apply.
        Assert.True(result.ScoreByDriverId[driverIds[0]] < new DriverScoringOptions().MinimumScore);
    }

    [Fact]
    public async Task SelectAsync_LeavesTheScoreRejectedDriverOutWhenTheKeptFleetIsBigEnough()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds, capacitiesKg: [1500m, 3000m]);
        var users = CandidateRepository([
            Candidate(driverIds[0], successes: 0, pending: 5, inProgress: 5),
            Candidate(driverIds[1], successes: 10, pending: 0, inProgress: 0)
        ]);

        var result = await Filter(users).SelectAsync(
            pool.Drivers,
            pool.VehiclesById,
            ShipmentsOf(1400m, 900m),
            SpreadMatrix().Distances,
            SpreadMatrix().Durations,
            DayStart,
            DayEnd);

        // The best driver can carry the 2300 kg alone, so the threshold is left to stand.
        var excluded = Assert.Single(result.ExcludedDrivers);
        Assert.Equal(DriverIneligibilityReason.ScoreBelowMinimum, excluded.Reason);
        Assert.Equal([driverIds[1]], result.KeptDrivers.Select(d => d.DriverId));
    }

    [Fact]
    public async Task SelectAsync_NeverResurrectsADriverRejectedForCapacity()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds, capacitiesKg: [1500m, 5m]);
        var users = CandidateRepository([
            Candidate(driverIds[0], successes: 10, pending: 0, inProgress: 0),
            Candidate(driverIds[1], successes: 10, pending: 0, inProgress: 0)
        ]);

        var result = await Filter(users).SelectAsync(
            pool.Drivers,
            pool.VehiclesById,
            ShipmentsOf(1400m, 1200m, 840m),
            SpreadMatrix().Distances,
            SpreadMatrix().Durations,
            DayStart,
            DayEnd);

        // Capacity is short by a wide margin and the backfill wants more drivers, but a vehicle
        // that cannot carry the heaviest shipment is not a scoring question.
        var excluded = Assert.Single(result.ExcludedDrivers);
        Assert.Equal(driverIds[1], excluded.DriverId);
        Assert.Equal(DriverIneligibilityReason.InsufficientVehicleCapacity, excluded.Reason);
        Assert.Equal([driverIds[0]], result.KeptDrivers.Select(d => d.DriverId));
    }

    [Fact]
    public async Task SelectAsync_WithADriverMissingItsCandidateRow_StillScoresEveryone()
    {
        Guid[] driverIds = [Guid.NewGuid(), Guid.NewGuid()];
        var pool = Pool(driverIds);

        // Only the first driver has a row, so the second one has no cost to report.
        var users = CandidateRepository([Candidate(driverIds[0], successes: 10, pending: 0)]);

        var result = await Filter(users).SelectAsync(
            pool.Drivers, pool.VehiclesById, Shipments(), Matrix(2), Matrix(2), DayStart, DayEnd);

        Assert.Equal(driverIds, result.KeptDrivers.Select(d => d.DriverId));
        Assert.All(result.KeptDrivers, d => Assert.True(d.Score > 0m));
    }

    private static DriverEligibilityFilter Filter(
        Mock<IUserRepository> users,
        DriverScoringOptions? options = null) =>
        new(users.Object, (options ?? new DriverScoringOptions()).ToPolicy());

    private static Mock<IUserRepository> CandidateRepository(
        IReadOnlyList<DriverAssignmentCandidate> candidates)
    {
        var users = new Mock<IUserRepository>(MockBehavior.Strict);

        users
            .Setup(u => u.GetDriverCandidatesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        return users;
    }

    private static IReadOnlyList<PlanningShipmentData> Shipments(decimal weightKg = 1m) =>
        [new PlanningShipmentData(Guid.NewGuid(), ShipmentPriorityScoring.NormalUrgency, weightKg, new Coordinate(-34.62m, -58.40m), null)];

    /// <summary>
    /// Several shipments, which is what the capacity requirement is summed over.
    /// </summary>
    private static IReadOnlyList<PlanningShipmentData> ShipmentsOf(params decimal[] weightsKg) =>
        weightsKg
            .Select(weightKg => new PlanningShipmentData(
                Guid.NewGuid(),
                ShipmentPriorityScoring.NormalUrgency,
                weightKg,
                new Coordinate(-34.62m, -58.40m),
                null))
            .ToList();

    private static DriverPool Pool(Guid[] driverIds, decimal[]? capacitiesKg = null)
    {
        var vehiclesById = new Dictionary<Guid, Vehicle>();
        var drivers = new List<PlanningDriverData>(driverIds.Length);
        var candidates = new List<DriverAssignmentCandidate>(driverIds.Length);

        for (var index = 0; index < driverIds.Length; index++)
        {
            var capacity = capacitiesKg is null ? 500m : capacitiesKg[index];
            var vehicle = Vehicle.Create($"AA-{index:000}-AA", capacityKg: capacity);

            vehiclesById[vehicle.Id] = vehicle;
            drivers.Add(new PlanningDriverData(
                driverIds[index],
                new Coordinate(-34.60m, -58.38m),
                vehicle.Id,
                Guid.NewGuid(),
                Score: 0m));
            candidates.Add(Candidate(driverIds[index]));
        }

        return new DriverPool(drivers, vehiclesById, candidates);
    }

    private sealed record DriverPool(
        IReadOnlyList<PlanningDriverData> Drivers,
        IReadOnlyDictionary<Guid, Vehicle> VehiclesById,
        IReadOnlyList<DriverAssignmentCandidate> Candidates);

    private static DriverAssignmentCandidate Candidate(
        Guid driverId,
        int successes = 0,
        int pending = 0,
        int inProgress = 0) =>
        new(
            driverId,
            new Coordinate(-34.60m, -58.38m),
            500m,
            successes,
            pending,
            inProgress,
            InProgressShipmentWeightKg: 0m,
            SalaryPerHour: 0m,
            KilometersPerDay: 0m);

    /// <summary>
    /// One node per driver plus one per shipment, which is the shape the factory reads: driver
    /// rows against the shipment columns.
    /// </summary>
    private static RouteMatrix Matrix(int driverCount, int shipmentCount = 1)
    {
        var size = driverCount + shipmentCount;
        return RouteMatrix.Create(size, new long[size * size]);
    }

    /// <summary>
    /// Five nodes for two drivers and three shipments, with the second driver sitting next to the
    /// shipments and the first one far away. An all zero matrix leaves every driver tied, which
    /// hides whether the thresholds did anything.
    /// </summary>
    private static (RouteMatrix Distances, RouteMatrix Durations) SpreadMatrix()
    {
        var distances = RouteMatrix.Create(
            5,
            [
                0, 900, 900, 900, 900,
                900, 0, 200, 200, 200,
                900, 200, 0, 200, 200,
                900, 200, 200, 0, 200,
                900, 200, 200, 200, 0
            ]);

        var durations = RouteMatrix.Create(
            5,
            [
                0, 900, 900, 900, 900,
                900, 0, 120, 120, 120,
                900, 120, 0, 120, 120,
                900, 120, 120, 0, 120,
                900, 120, 120, 120, 0
            ]);

        return (distances, durations);
    }
}