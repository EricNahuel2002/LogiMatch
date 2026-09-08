using Application.Dtos.Shipments;

namespace Application.Services;

public interface IShipmentAssignmentService
{
    Task<DriverAssignmentSuggestion> SuggestDriverAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);
}