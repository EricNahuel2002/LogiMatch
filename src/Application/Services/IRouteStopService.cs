using Application.Dtos.RouteStops;

namespace Application.Services;

public interface IRouteStopService
{
    Task<Guid> CreateAsync(CreateRouteStopRequest request, CancellationToken cancellationToken = default);
}