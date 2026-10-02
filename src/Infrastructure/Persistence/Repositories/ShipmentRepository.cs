using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ShipmentRepository : IShipmentRepository
{
    private readonly LogiMatchDbContext _db;

    public ShipmentRepository(LogiMatchDbContext db)
    {
        _db = db;
    }

    public Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Shipments.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public Task<Shipment?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Shipments
            .Include(s => s.Order)
            .Include(s => s.DeliveryAttempts)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public Task<Shipment?> GetByIdWithAssignmentDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Shipments
            .Include(s => s.Order)
                .ThenInclude(o => o.Items)
            .Include(s => s.RouteStop)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingShipmentPriorityData>> GetPendingForPriorityAsync(
        CancellationToken cancellationToken = default)
    {
        var shipments = await _db.Shipments
            .Where(s => s.Status == ShipmentStatus.Pending)
            .Include(s => s.RouteStop!)
                .ThenInclude(rs => rs.Route)
            .Include(s => s.Order)
                .ThenInclude(o => o.Items)
            .ToListAsync(cancellationToken);

        if (shipments.Count == 0)
        {
            return [];
        }

        var customerIds = shipments
            .Select(s => s.Order.CustomerId)
            .Distinct()
            .ToList();

        var outcomes = await _db.Shipments
            .Where(s => (s.Status == ShipmentStatus.DeliveryFailed || s.Status == ShipmentStatus.Finalized)
                && customerIds.Contains(s.Order.CustomerId))
            .GroupBy(s => s.Order.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                DeliveryFailedCount = g.Count(s => s.Status == ShipmentStatus.DeliveryFailed),
                FinalizedCount = g.Count(s => s.Status == ShipmentStatus.Finalized)
            })
            .ToDictionaryAsync(
                o => o.CustomerId,
                o => (DeliveryFailed: o.DeliveryFailedCount, Finalized: o.FinalizedCount),
                cancellationToken);

        return shipments
            .Select(s =>
            {
                outcomes.TryGetValue(s.Order.CustomerId, out var counts);

                return new PendingShipmentPriorityData(
                    s,
                    s.RouteStop?.Route?.Origin,
                    s.RouteStop?.Coordinate,
                    s.Order.Items.Sum(i => i.WeightKg * i.Quantity),
                    s.Order.DeliveryWindowStartAt is { } start && s.Order.DeliveryWindowEndAt is { } end
                        ? (double?)(end - start).TotalMinutes
                        : null,
                    counts.DeliveryFailed,
                    counts.Finalized);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<Shipment>> GetPendingWithDriverAndRiskAsync(
        CancellationToken cancellationToken = default)
    {
        var shipments = await _db.Shipments
            .Where(s => s.Status == ShipmentStatus.Pending)
            .Include(s => s.Order)
                .ThenInclude(o => o.AssignedDriver)
            .Include(s => s.RouteStop!)
                .ThenInclude(rs => rs.Route!)
                .ThenInclude(r => r.Driver)
            .ToListAsync(cancellationToken);

        return shipments
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<Shipment>> GetAssignedWithHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Shipments
            .Where(s => s.Order.AssignedDriverId != null)
            .Include(s => s.Order)
                .ThenInclude(o => o.AssignedDriver)
            .Include(s => s.History)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShipmentPlanningData>> GetPendingWithPlanningDetailsAsync(
        CancellationToken cancellationToken = default)
    {
        // The delivery coordinate lives on the owned RouteStop, so it is projected as two
        // nullable decimals and rebuilt in memory: a shipment without a route stop has to come
        // back as a null destination instead of collapsing to the (0,0) default.
        var rows = await _db.Shipments
            .AsNoTracking()
            .Where(s => s.Status == ShipmentStatus.Pending)
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Priority,
                WeightKg = s.Order.Items.Sum(i => i.WeightKg * i.Quantity),
                Latitude = (decimal?)s.RouteStop!.Coordinate.Latitude,
                Longitude = (decimal?)s.RouteStop!.Coordinate.Longitude,
                s.Order.DeliveryWindowStartAt,
                s.Order.DeliveryWindowEndAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ShipmentPlanningData(
                r.Id,
                r.Priority,
                r.WeightKg,
                r.Latitude.HasValue && r.Longitude.HasValue
                    ? new Coordinate(r.Latitude.Value, r.Longitude.Value)
                    : null,
                r.DeliveryWindowStartAt,
                r.DeliveryWindowEndAt))
            .ToList();
    }

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        await _db.Shipments.AddAsync(shipment, cancellationToken);
    }
}