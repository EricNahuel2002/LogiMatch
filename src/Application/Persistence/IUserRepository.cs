using Domain.Entities;

namespace Application.Persistence;

public interface IUserRepository
{
    Task<Customer?> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Admin?> GetAdminByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Driver?> GetDriverByIdAsync(Guid id, CancellationToken cancellationToken = default);
}