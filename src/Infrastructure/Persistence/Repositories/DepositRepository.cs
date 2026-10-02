using Application.Persistence;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class DepositRepository : IDepositRepository
{
    private readonly LogiMatchDbContext _db;

    public DepositRepository(LogiMatchDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Deposit>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Deposits
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
