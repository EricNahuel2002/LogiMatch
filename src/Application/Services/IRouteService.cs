using Application.Dtos.Routes;

namespace Application.Services;

public interface IRouteService
{
    Task<Guid> CreateAsync(CreateRouteRequest request, CancellationToken cancellationToken = default);

    Task AssignDriverAsync(Guid routeId, Guid driverId, CancellationToken cancellationToken = default);

    Task AssignVehicleAsync(Guid routeId, Guid vehicleId, CancellationToken cancellationToken = default);

    Task AddStopAsync(Guid routeId, Guid routeStopId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RouteSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<RouteDetailsDto?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default);

    Task<CommitFromProposalResponse> CommitFromProposalAsync(CommitFromProposalRequest request, CancellationToken cancellationToken = default);
}