using Application.Integrations;
using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;

namespace Application.Services;

public class DelayRiskService : IDelayRiskService
{
    private static readonly TimeZoneInfo ArgentinaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private readonly IShipmentRepository _shipments;
    private readonly IRouteRepository _routes;
    private readonly IRouteClient _routeClient;
    private readonly IUnitOfWork _unitOfWork;

    public DelayRiskService(
        IShipmentRepository shipments,
        IRouteRepository routes,
        IRouteClient routeClient,
        IUnitOfWork unitOfWork)
    {
        _shipments = shipments;
        _routes = routes;
        _routeClient = routeClient;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteRecalculationAsync(CancellationToken cancellationToken = default)
    {
        await UpdateAverageTimeBetweenOrdersAsync(cancellationToken);
        await CalculateDelayRiskAsync(cancellationToken);
    }

    private async Task UpdateAverageTimeBetweenOrdersAsync(CancellationToken cancellationToken)
    {
        var shipments = await _shipments.GetAssignedWithHistoryAsync(cancellationToken);
        if (shipments.Count == 0)
        {
            return;
        }

        foreach (var group in shipments
            .Where(s => s.Order.AssignedDriver is not null)
            .GroupBy(s => s.Order.AssignedDriver!))
        {
            var driver = group.Key;
            var average = DriverAverageDeliveryTimeCalculator.ComputeAverage(group);
            driver.SetAverageTimeBetweenOrders(average);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task CalculateDelayRiskAsync(CancellationToken cancellationToken)
    {
        var routes = await _routes.GetActiveRoutesForDelayRiskAsync(cancellationToken);
        if (routes.Count == 0)
        {
            return;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ArgentinaTimeZone);

        foreach (var route in routes)
        {
            var driver = route.Driver;
            if (driver is null || driver.CurrentLocation is not { } location)
            {
                continue;
            }

            var currentStop = route.RouteStops
                .Where(rs => !IsTerminal(rs.Shipment.Status))
                .OrderBy(rs => rs.StopOrder)
                .FirstOrDefault();

            if (currentStop is null)
            {
                continue;
            }

            var shipmentsAhead = route.RouteStops
                .Count(rs => rs.StopOrder > currentStop.StopOrder && !IsTerminal(rs.Shipment.Status));

            var origin = new RouteOrigin(driver.Id, location);
            var metrics = await _routeClient.GetDrivingMetricsAsync(
                new[] { origin }, currentStop.Coordinate, cancellationToken);

            if (!metrics.TryGetValue(driver.Id, out var routeMetrics))
            {
                continue;
            }

            var estimatedArrivalAt = nowLocal.AddMinutes(routeMetrics.DurationMinutes);
            var remainingKilometers = routeMetrics.DistanceMeters / 1000m;

            var result = DeliveryDelayRiskCalculator.Evaluate(new DeliveryDelayRiskInput(
                currentStop.Shipment.Order.DeliveryWindowEndAt,
                estimatedArrivalAt,
                routeMetrics.DurationMinutes,
                remainingKilometers,
                shipmentsAhead,
                driver.AverageTimeBetweenOrders));

            currentStop.Shipment.SetDelayRiskPercentage(result.RiskIndex);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool IsTerminal(ShipmentStatus status)
    {
        return status is ShipmentStatus.Finalized
            or ShipmentStatus.DeliveryFailed
            or ShipmentStatus.Cancelled;
    }
}