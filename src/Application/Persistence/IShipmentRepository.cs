using Domain.Entities;

namespace Application.Persistence;

public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Shipment?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Shipment?> GetByIdWithAssignmentDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingShipmentPriorityData>> GetPendingForPriorityAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shipment>> GetPendingWithDriverAndRiskAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shipment>> GetAssignedWithHistoryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the planner read model for every pending shipment in a single query, ordered by
    /// descending priority and then by creation date so the node order the solver receives is
    /// the same on every run.
    /// </summary>
    Task<IReadOnlyList<ShipmentPlanningData>> GetPendingWithPlanningDetailsAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default);
}