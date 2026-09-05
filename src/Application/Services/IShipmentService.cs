using Application.Dtos.Shipments;

namespace Application.Services;

public interface IShipmentService
{
    Task<Guid> CreateAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task StartAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    Task StopAsync(Guid shipmentId, StopShipmentRequest request, CancellationToken cancellationToken = default);

    Task ResumeAsync(Guid shipmentId, ResumeShipmentRequest request, CancellationToken cancellationToken = default);

    Task MarkArrivedAsync(Guid shipmentId, MarkArrivedRequest request, CancellationToken cancellationToken = default);

    Task CancelAsync(Guid shipmentId, CancelShipmentRequest request, CancellationToken cancellationToken = default);

    Task RegisterDeliveryAttemptAsync(
        Guid shipmentId,
        RegisterDeliveryAttemptRequest request,
        CancellationToken cancellationToken = default);
}