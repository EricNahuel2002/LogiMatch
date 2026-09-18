using Domain.Entities;
using Domain.Enums;

namespace Domain.Services;

public sealed record ShipmentTimeRange(DateTime? StartedAt, DateTime? FinalizedAt);

public static class DriverAverageDeliveryTimeCalculator
{
    public static TimeSpan? ComputeAverage(IEnumerable<Shipment> shipments)
    {
        ArgumentNullException.ThrowIfNull(shipments);

        var ranges = shipments
            .Select(ToTimeRange)
            .Where(r => r.StartedAt.HasValue && r.FinalizedAt.HasValue)
            .OrderBy(r => r.StartedAt)
            .ToList();

        if (ranges.Count < 2)
        {
            return null;
        }

        var total = TimeSpan.Zero;
        var intervalCount = 0;

        for (var i = 1; i < ranges.Count; i++)
        {
            var difference = ranges[i].StartedAt!.Value - ranges[i - 1].FinalizedAt!.Value;
            if (difference < TimeSpan.Zero)
            {
                continue;
            }

            total += difference;
            intervalCount++;
        }

        return intervalCount == 0 ? null : TimeSpan.FromTicks(total.Ticks / intervalCount);
    }

    public static ShipmentTimeRange ToTimeRange(Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        var history = shipment.History
            .OrderBy(h => h.RecordedAt)
            .ToList();

        var startedAt = history
            .FirstOrDefault(h => h.Status == ShipmentStatus.InProgress)
            ?.RecordedAt;

        var finalizedAt = history
            .FirstOrDefault(h => h.Status == ShipmentStatus.Finalized)
            ?.RecordedAt;

        return new ShipmentTimeRange(startedAt, finalizedAt);
    }
}