namespace Application.Integrations;

/// <summary>
/// Publishes route changes to a driver in real time. Implemented outside the Application layer so
/// the transport (SignalR) stays out of the business rules.
/// </summary>
public interface IDriverRealtimeNotifier
{
    Task RouteAssignedAsync(Guid driverId, Guid routeId, CancellationToken cancellationToken = default);

    Task RouteCancelledAsync(Guid driverId, Guid routeId, CancellationToken cancellationToken = default);
}
