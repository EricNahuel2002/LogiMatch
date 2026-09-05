using Application.Persistence;
using Domain.Entities;
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
}