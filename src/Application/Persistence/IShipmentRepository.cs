using Domain.Entities;

namespace Application.Persistence;

public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Shipment?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Shipment?> GetByIdWithAssignmentDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingShipmentPriorityData>> GetPendingForPriorityAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shipment>> GetAssignedWithHistoryAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default);
}