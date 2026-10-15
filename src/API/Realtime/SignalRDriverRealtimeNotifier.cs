using Api.Hubs;
using Application.Integrations;
using Microsoft.AspNetCore.SignalR;

namespace Api.Realtime;

public sealed class SignalRDriverRealtimeNotifier : IDriverRealtimeNotifier
{
    private readonly IHubContext<DriverHub> _hubContext;

    public SignalRDriverRealtimeNotifier(IHubContext<DriverHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task RouteAssignedAsync(Guid driverId, Guid routeId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(DriverHub.GroupName(driverId))
            .SendAsync(DriverHub.RouteAssignedEvent, new { routeId }, cancellationToken);

    public Task RouteCancelledAsync(Guid driverId, Guid routeId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(DriverHub.GroupName(driverId))
            .SendAsync(DriverHub.RouteCancelledEvent, new { routeId }, cancellationToken);
}
