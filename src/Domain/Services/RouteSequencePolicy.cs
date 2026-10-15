using Domain.Enums;

namespace Domain.Services;

/// <summary>
/// Owns the rule that decides which stop of a route the driver is allowed to work on next.
/// The current stop is the non-terminal stop with the lowest stop order, so a driver can only
/// advance a shipment once every earlier stop has reached a terminal status.
/// </summary>
public static class RouteSequencePolicy
{
    public static bool IsTerminal(ShipmentStatus status) =>
        status is ShipmentStatus.Finalized
            or ShipmentStatus.DeliveryFailed
            or ShipmentStatus.Cancelled;

    /// <summary>
    /// Returns the id of the shipment on the current stop, or null when every stop is terminal.
    /// </summary>
    public static Guid? GetCurrentStopShipmentId(IEnumerable<RouteStopProgress> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);

        return stops
            .Where(stop => !IsTerminal(stop.Status))
            .OrderBy(stop => stop.StopOrder)
            .Select(stop => (Guid?)stop.ShipmentId)
            .FirstOrDefault();
    }

    /// <summary>
    /// True when the given shipment is the one the driver must be working on right now.
    /// </summary>
    public static bool IsCurrentStop(IEnumerable<RouteStopProgress> stops, Guid shipmentId)
    {
        ArgumentNullException.ThrowIfNull(stops);

        var current = GetCurrentStopShipmentId(stops);
        return current is { } id && id == shipmentId;
    }
}

public readonly record struct RouteStopProgress(Guid ShipmentId, int StopOrder, ShipmentStatus Status);
