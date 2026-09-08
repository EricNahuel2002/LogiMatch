using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly LogiMatchDbContext _db;

    public UserRepository(LogiMatchDbContext db)
    {
        _db = db;
    }

    public Task<Customer?> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Users.OfType<Customer>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<Admin?> GetAdminByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Users.OfType<Admin>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<Driver?> GetDriverByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _db.Users.OfType<Driver>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DriverAssignmentCandidate>> GetDriverCandidatesAsync(
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        CancellationToken cancellationToken = default)
    {
        var drivers = await _db.Users.OfType<Driver>()
            .AsNoTracking()
            .Select(d => new { d.Id, d.CurrentLocation })
            .ToListAsync(cancellationToken);

        var driverIds = drivers.Select(d => d.Id).ToList();

        var capacities = await _db.Vehicles
            .Where(v => v.Active)
            .SelectMany(v => v.Drivers.Select(d => new { d.Id, v.CapacityKg }))
            .GroupBy(x => x.Id)
            .Select(g => new { DriverId = g.Key, MaxCapacityKg = g.Max(x => x.CapacityKg) })
            .ToListAsync(cancellationToken);

        var attemptsToday = await _db.DeliveryAttempts
            .Where(a => a.Succeeded
                && a.AttemptedAt >= dayStartUtc
                && a.AttemptedAt < dayEndUtc
                && a.Order.AssignedDriverId != null
                && driverIds.Contains(a.Order.AssignedDriverId.Value))
            .GroupBy(a => a.Order.AssignedDriverId!.Value)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pending = await _db.Shipments
            .Where(s => s.Status == ShipmentStatus.Pending
                && s.Order.AssignedDriverId != null
                && driverIds.Contains(s.Order.AssignedDriverId.Value))
            .GroupBy(s => s.Order.AssignedDriverId!.Value)
            .Select(g => new { DriverId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var inProgress = await _db.Shipments
            .Where(s => s.Status == ShipmentStatus.InProgress
                && s.Order.AssignedDriverId != null
                && driverIds.Contains(s.Order.AssignedDriverId.Value))
            .Select(s => new
            {
                s.Order.AssignedDriverId,
                TotalWeightKg = s.Order.Items.Sum(i => i.WeightKg * i.Quantity)
            })
            .ToListAsync(cancellationToken);

        var capacityByDriver = capacities.ToDictionary(x => x.DriverId, x => x.MaxCapacityKg);
        var attemptsByDriver = attemptsToday.ToDictionary(x => x.DriverId, x => x.Count);
        var pendingByDriver = pending.ToDictionary(x => x.DriverId, x => x.Count);

        var inProgressByDriver = inProgress
            .GroupBy(x => x.AssignedDriverId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new { Count = g.Count(), WeightKg = g.Sum(x => x.TotalWeightKg) });

        var results = drivers.Select(d =>
        {
            var inProgressMetrics = inProgressByDriver.TryGetValue(d.Id, out var metrics)
                ? metrics
                : new { Count = 0, WeightKg = 0m };

            return new DriverAssignmentCandidate(
                d.Id,
                d.CurrentLocation,
                capacityByDriver.GetValueOrDefault(d.Id),
                attemptsByDriver.GetValueOrDefault(d.Id),
                pendingByDriver.GetValueOrDefault(d.Id),
                inProgressMetrics.Count,
                inProgressMetrics.WeightKg);
        }).ToList();

        return results;
    }
}