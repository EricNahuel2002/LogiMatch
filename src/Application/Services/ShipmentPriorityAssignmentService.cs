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
                p.AbsentDeliveriesCount,
                p.SucceededDeliveriesCount))
            .ToList();

        var results = ShipmentPriorityScoring.Assign(inputs);
        var shipmentById = pending.ToDictionary(p => p.Shipment.Id, p => p.Shipment);

        foreach (var result in results)
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