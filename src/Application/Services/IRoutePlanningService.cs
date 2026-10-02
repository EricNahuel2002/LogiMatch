using Application.Dtos.RoutePlanning;

namespace Application.Services;

public interface IRoutePlanningService
{
    /// <summary>
    /// Builds a read-only routing proposal for the requested drivers, vehicles and deposits
    /// over every pending shipment. Pending shipments that cannot be routed come back in
    /// <see cref="RoutePlanningProposalResponse.ExcludedShipments" /> with their reason instead
    /// of failing the preview. Nothing is persisted: the caller decides what to do with the
    /// proposal.
    /// </summary>
    Task<RoutePlanningProposalResponse> PreviewAsync(
        PlanRoutesRequest request,
        CancellationToken cancellationToken = default);
}
