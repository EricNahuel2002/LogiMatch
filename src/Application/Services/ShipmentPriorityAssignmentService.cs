using Application.Integrations;
using Application.Persistence;
using Domain.Services;

namespace Application.Services;

public class ShipmentPriorityAssignmentService : IShipmentPriorityAssignmentService
{
    private readonly IShipmentRepository _shipments;
    private readonly IRouteClient _routeClient;
    private readonly IUnitOfWork _unitOfWork;

    public ShipmentPriorityAssignmentService(
        IShipmentRepository shipments,
        IRouteClient routeClient,
        IUnitOfWork unitOfWork)
    {
        _shipments = shipments;
        _routeClient = routeClient;
        _unitOfWork = unitOfWork;
    }

    public async Task RecalculatePrioritiesAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _shipments.GetPendingForPriorityAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        var distanceByShipment = await ResolveDistancesAsync(pending, cancellationToken);

        var inputs = pending
            .Select(p => new ShipmentPriorityInput(
                p.Shipment.Id,
                p.WindowSpanMinutes,
                distanceByShipment.TryGetValue(p.Shipment.Id, out var distance) ? distance : null,
                p.WeightKg,
                p.DeliveryFailedCount,
                p.FinalizedCount))
            .ToList();

        var shipmentById = pending.ToDictionary(p => p.Shipment.Id, p => p.Shipment);

        var urgentPending = pending
            .Where(p => p.Shipment.Priority == Domain.Enums.ShipmentPriority.Urgent)
            .ToList();
        var nonUrgentPending = pending
            .Where(p => p.Shipment.Priority != Domain.Enums.ShipmentPriority.Urgent)
            .ToList();

        var combinedResults = new List<(Guid ShipmentId, Domain.Enums.ShipmentPriority Priority, decimal Score)>();

        if (urgentPending.Count > 0)
        {
            var urgentInputs = inputs
                .Where(i => urgentPending.Any(u => u.Shipment.Id == i.ShipmentId))
                .ToList();
            var urgentScores = ShipmentPriorityScoring.Assign(urgentInputs);
            foreach (var scoreResult in urgentScores)
            {
                combinedResults.Add((scoreResult.ShipmentId, Domain.Enums.ShipmentPriority.Urgent, scoreResult.Score));
            }
        }

        if (nonUrgentPending.Count > 0)
        {
            var nonUrgentInputs = inputs
                .Where(i => nonUrgentPending.Any(n => n.Shipment.Id == i.ShipmentId))
                .ToList();
            var nonUrgentScores = ShipmentPriorityScoring.Assign(nonUrgentInputs);
            foreach (var scoreResult in nonUrgentScores)
            {
                var priority = ShipmentPriorityScoring.ToPriority(scoreResult.Score);
                combinedResults.Add((scoreResult.ShipmentId, priority, scoreResult.Score));
            }
        }

        foreach (var result in combinedResults)
        {
            if (shipmentById.TryGetValue(result.ShipmentId, out var shipment))
            {
                shipment.SetPriority(result.Priority);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, int>> ResolveDistancesAsync(
        IReadOnlyList<PendingShipmentPriorityData> pending,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, int>();

        var routable = pending
            .Where(p => p.RouteOrigin is not null && p.Destination is not null)
            .ToList();

        foreach (var destinationGroup in routable.GroupBy(p => p.Destination!))
        {
            var origins = destinationGroup
                .Select(p => new RouteOrigin(p.Shipment.Id, p.RouteOrigin!))
                .ToList();

            var metrics = await _routeClient.GetDrivingMetricsAsync(
                origins, destinationGroup.Key, cancellationToken);

            foreach (var (shipmentId, routeMetrics) in metrics)
            {
                result[shipmentId] = routeMetrics.DistanceMeters;
            }
        }

        return result;
    }
}