namespace Application.Services;

public interface IShipmentPriorityAssignmentService
{
    Task RecalculatePrioritiesAsync(CancellationToken cancellationToken = default);
}