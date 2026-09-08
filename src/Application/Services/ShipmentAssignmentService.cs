using Application.Dtos.Shipments;
using Application.Exceptions;
using Application.Integrations;
using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

namespace Application.Services;

public class ShipmentAssignmentService : IShipmentAssignmentService
{
    private static readonly TimeZoneInfo ArgentinaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private readonly IShipmentRepository _shipments;
    private readonly IUserRepository _users;
    private readonly IRouteClient _routeClient;

    public ShipmentAssignmentService(
        IShipmentRepository shipments,
        IUserRepository users,
        IRouteClient routeClient)
    {
        _shipments = shipments;
        _users = users;
        _routeClient = routeClient;
    }

    public async Task<DriverAssignmentSuggestion> SuggestDriverAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        var shipment = await _shipments.GetByIdWithAssignmentDetailsAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shipment), shipmentId);

        if (shipment.Status != ShipmentStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending shipment can be evaluated for driver suggestion.");
        }

        if (shipment.RouteStop is null)
        {
            throw new InvalidOperationException("The shipment must have a route stop before evaluating driver suggestion.");
        }

        var shipmentWeightKg = shipment.Order.Items.Sum(item => item.WeightKg * item.Quantity);

        var (dayStartUtc, dayEndUtc) = GetCurrentArgentinaDayUtc();
        var candidates = await _users.GetDriverCandidatesAsync(dayStartUtc, dayEndUtc, cancellationToken);

        var eligible = new List<(Guid DriverId, Coordinate Location, DriverScoringInput Input)>();

        foreach (var candidate in candidates)
        {
            if (candidate.CurrentLocation is not { } location)
            {
                continue;
            }

            if (candidate.MaxActiveVehicleCapacityKg <= 0)
            {
                continue;
            }

            var freeCapacityKg = candidate.MaxActiveVehicleCapacityKg - candidate.InProgressShipmentWeightKg;
            if (freeCapacityKg < shipmentWeightKg)
            {
                continue;
            }

            eligible.Add((
                candidate.DriverId,
                location,
                new DriverScoringInput(
                    candidate.DriverId,
                    candidate.SuccessAttemptsToday,
                    candidate.PendingShipmentCount,
                    candidate.InProgressShipmentCount,
                    0,
                    0,
                    freeCapacityKg)));
        }

        if (eligible.Count == 0)
        {
            throw new InvalidOperationException("No eligible drivers available for the shipment.");
        }

        var origins = eligible
            .Select(e => new RouteOrigin(e.DriverId, e.Location))
            .ToList();

        var metrics = await _routeClient.GetDrivingMetricsAsync(
            origins, shipment.RouteStop.Coordinate, cancellationToken);

        var inputs = new List<DriverScoringInput>();
        foreach (var (driverId, _, input) in eligible)
        {
            if (metrics.TryGetValue(driverId, out var routeMetrics))
            {
                inputs.Add(input with
                {
                    DistanceMeters = routeMetrics.DistanceMeters,
                    DurationMinutes = routeMetrics.DurationMinutes
                });
            }
        }

        if (inputs.Count == 0)
        {
            throw new InvalidOperationException("No eligible drivers available for the shipment.");
        }

        var ranked = DriverScoring.Rank(inputs);
        var recommendedDriverId = ranked[0].DriverId;

        var ranking = ranked
            .Select(result => new DriverRankingItem(
                result.DriverId,
                result.Score,
                result.Input.SuccessAttemptsToday,
                result.Input.PendingShipmentCount,
                result.Input.InProgressShipmentCount,
                result.Input.DistanceMeters,
                result.Input.DurationMinutes,
                result.Input.FreeCapacityKg))
            .ToList();

        return new DriverAssignmentSuggestion(shipmentId, recommendedDriverId, ranking);
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetCurrentArgentinaDayUtc()
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ArgentinaTimeZone);
        var dayStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day);

        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, ArgentinaTimeZone);
        return (dayStartUtc, dayStartUtc.AddDays(1));
    }
}