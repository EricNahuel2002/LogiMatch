using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
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
                .ThenInclude(o => o.Customer)
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
            .Include(s => s.Order)
                .ThenInclude(o => o.Customer)
            .ToListAsync(cancellationToken);

        return shipments
            .Select(s => new PendingShipmentPriorityData(
                s,
                s.RouteStop?.Route?.Origin,
                s.RouteStop?.Coordinate,
                s.Order.Items.Sum(i => i.WeightKg * i.Quantity),
                s.Order.DeliveryWindowStartAt is { } start && s.Order.DeliveryWindowEndAt is { } end
                    ? (double?)(end - start).TotalMinutes
                    : null,
                s.Order.Customer.AbsentDeliveriesCount,
                s.Order.Customer.SucceededDeliveriesCount))
            .ToList();
    }

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        await _db.Shipments.AddAsync(shipment, cancellationToken);
    }
}