using Application.Persistence;
using Application.RoutePlanning;
using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

namespace UnitTests.Application;

public class DriverScoringInputFactoryTests
{
    [Fact]
    public void Build_AveragesDistanceAndDurationOverEveryShipment()
    {
        // 2 drivers, 2 shipments, 1 deposit.
        var distance = Matrix(
            5,
            [
                0, 100, 300, 500, 900,
                100, 0, 200, 400, 800,
                300, 200, 0, 100, 700,
                500, 400, 100, 0, 600,
                900, 800, 700, 600, 0
            ]);

        var duration = Matrix(
            5,
            [
                0, 60, 180, 600, 3600,
                60, 0, 120, 540, 3540,
                180, 120, 0, 300, 3420,
                600, 540, 300, 0, 3000,
                3600, 3540, 3420, 3000, 0
            ]);

        var pool = Pool(driverCount: 2);

        var inputs = DriverScoringInputFactory.Build(
            pool.Drivers,
            Candidates(),
            pool.Vehicles,
            distance,
            duration,
            shipmentNodeOffset: 2,
            shipmentCount: 2);

        // Driver 0 sits 300 and 500 from the shipments: 400 m. Seconds 180 and 600: 390 s, which
        // is 6.5 minutes and must round up rather than truncate.
        Assert.Equal(pool.DriverIds[0], inputs[0].DriverId);
        Assert.Equal(400, inputs[0].DistanceMeters);
        Assert.Equal(7, inputs[0].DurationMinutes);

        // Driver 1 sits 200 and 400: 300 m. Seconds 120 and 540: 330 s, 5.5 minutes, rounds to 6.
        Assert.Equal(pool.DriverIds[1], inputs[1].DriverId);
        Assert.Equal(300, inputs[1].DistanceMeters);
        Assert.Equal(6, inputs[1].DurationMinutes);
    }

    [Fact]
    public void Build_SkipsUnreachableShipmentsInsteadOfAveragingThem()
    {
        var pool = Pool(driverCount: 1);
        var matrix = Matrix(
            3,
            [
                0, RouteMatrix.UnreachableValue, 2000,
                RouteMatrix.UnreachableValue, 0, 3000,
                900, 700, 0
            ],
            hasCompleteData: false);

        var inputs = DriverScoringInputFactory.Build(
            pool.Drivers,
            Candidates(),
            pool.Vehicles,
            matrix,
            matrix,
            shipmentNodeOffset: 1,
            shipmentCount: 1);

        // The only shipment cell is unreachable, so there is nothing left to average. Letting the
        // unreachable value in would dominate the score instead.
        Assert.Equal(0, inputs[0].DistanceMeters);
    }

    [Fact]
    public void Build_WithoutShipments_ReturnsZeroDistance()
    {
        var pool = Pool(driverCount: 1);
        var matrix = Matrix(2, [0, 100, 100, 0]);

        var inputs = DriverScoringInputFactory.Build(
            pool.Drivers,
            Candidates(),
            pool.Vehicles,
            matrix,
            matrix,
            shipmentNodeOffset: 1,
            shipmentCount: 0);

        Assert.Equal(0, inputs[0].DistanceMeters);
        Assert.Equal(0, inputs[0].DurationMinutes);
    }

    [Fact]
    public void Build_ReportsFreeCapacityOfTheSelectedVehicle()
    {
        var pool = Pool(driverCount: 1, capacityKg: 800m);
        var driverId = pool.DriverIds[0];

        var inputs = DriverScoringInputFactory.Build(
            pool.Drivers,
            new Dictionary<Guid, DriverAssignmentCandidate>
            {
                [driverId] = Candidate(driverId, inProgressWeightKg: 300m)
            },
            pool.Vehicles,
            Matrix(2, [0, 100, 100, 0]),
            Matrix(2, [0, 60, 60, 0]),
            shipmentNodeOffset: 1,
            shipmentCount: 0);

        Assert.Equal(500m, inputs[0].FreeCapacityKg);
    }

    [Fact]
    public void Build_WithoutCandidateRow_ScoresNeutralMetrics()
    {
        var pool = Pool(driverCount: 1, capacityKg: 400m);

        var inputs = DriverScoringInputFactory.Build(
            pool.Drivers,
            new Dictionary<Guid, DriverAssignmentCandidate>(),
            pool.Vehicles,
            Matrix(2, [0, 100, 100, 0]),
            Matrix(2, [0, 60, 60, 0]),
            shipmentNodeOffset: 1,
            shipmentCount: 0);

        // Neutral, not zeroed: a driver with no history must not look like the worst driver on
        // every criterion just because it has no row.
        Assert.Equal(0, inputs[0].SuccessAttemptsToday);
        Assert.Equal(0, inputs[0].PendingShipmentCount);
        Assert.Equal(0, inputs[0].InProgressShipmentCount);
        Assert.Equal(400m, inputs[0].FreeCapacityKg);
        Assert.Null(inputs[0].EstimatedOperationCost);
    }

    [Fact]
    public void Build_WithoutCandidateRow_RanksAsNeutralOnCostNotAsCheapest()
    {
        // A zero here would be the best possible value on a criterion where lower is better, which
        // is the opposite of neutral.
        var pool = Pool(driverCount: 2, capacityKg: 400m);

        var inputs = DriverScoringInputFactory.Build(
            pool.Drivers,
            new Dictionary<Guid, DriverAssignmentCandidate>(),
            pool.Vehicles,
            Matrix(3, [0, 100, 100, 100, 0, 100, 100, 100, 0]),
            Matrix(3, [0, 60, 60, 60, 0, 60, 60, 60, 0]),
            shipmentNodeOffset: 2,
            shipmentCount: 0);

        var ranked = DriverScoring.Rank(inputs);

        Assert.Equal(ranked[0].Score, ranked[1].Score);
    }

    [Fact]
    public void Build_WithMissingVehicle_Throws()
    {
        var pool = Pool(driverCount: 1);
        var matrix = Matrix(2, [0, 100, 100, 0]);

        var error = Assert.Throws<InvalidOperationException>(() => DriverScoringInputFactory.Build(
            pool.Drivers,
            Candidates(),
            new Dictionary<Guid, Vehicle>(),
            matrix,
            matrix,
            shipmentNodeOffset: 1,
            shipmentCount: 0));

        Assert.Contains(pool.DriverIds[0].ToString(), error.Message);
    }

    [Fact]
    public void Build_KeepsTheSubmittedDriverOrder()
    {
        var pool = Pool(driverCount: 3);

        var inputs = DriverScoringInputFactory.Build(
            pool.Drivers,
            Candidates(),
            pool.Vehicles,
            Matrix(4, Increasing(4)),
            Matrix(4, Increasing(4)),
            shipmentNodeOffset: 3,
            shipmentCount: 1);

        // Driver order is node order in the routing problem, so it has to survive the mapping.
        Assert.Equal(pool.DriverIds, inputs.Select(i => i.DriverId));
    }

    private static IReadOnlyDictionary<Guid, DriverAssignmentCandidate> Candidates() =>
        new Dictionary<Guid, DriverAssignmentCandidate>();

    /// <summary>
    /// Driver ids paired with the vehicle each one is planning with, since the factory reads the
    /// capacity off the vehicle of the selection and not off the candidate row.
    /// </summary>
    private static DriverPool Pool(int driverCount, decimal capacityKg = 500m)
    {
        var driverIds = new Guid[driverCount];
        var vehiclesById = new Dictionary<Guid, Vehicle>();
        var vehicles = new List<Vehicle>(driverCount);
        var drivers = new List<PlanningDriverData>(driverCount);

        for (var index = 0; index < driverCount; index++)
        {
            var vehicle = Vehicle.Create($"AA-{index:000}-AA", capacityKg: capacityKg);
            driverIds[index] = Guid.NewGuid();

            vehicles.Add(vehicle);
            vehiclesById[vehicle.Id] = vehicle;
            drivers.Add(new PlanningDriverData(
                driverIds[index],
                new Coordinate(-34.60m, -58.38m),
                vehicle.Id,
                Guid.NewGuid(),
                Score: 0m));
        }

        return new DriverPool(driverIds, drivers, vehiclesById);
    }

    private sealed record DriverPool(
        Guid[] DriverIds,
        IReadOnlyList<PlanningDriverData> Drivers,
        IReadOnlyDictionary<Guid, Vehicle> Vehicles);

    private static DriverAssignmentCandidate Candidate(
        Guid driverId,
        decimal inProgressWeightKg = 0m) =>
        new(driverId, new Coordinate(-34.60m, -58.38m), 500m, 0, 0, 0, inProgressWeightKg);

    private static RouteMatrix Matrix(int size, long[] values, bool hasCompleteData = true) =>
        RouteMatrix.Create(size, values, hasCompleteData);

    private static long[] Increasing(int size)
    {
        var values = new long[size * size];

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                values[(row * size) + column] = row == column ? 0 : 100 * (Math.Abs(row - column) + 1);
            }
        }

        return values;
    }
}