using Domain.Entities;

namespace Application.Persistence;

public interface IUserRepository
{
    Task<Customer?> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Admin?> GetAdminByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Driver?> GetDriverByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the given drivers in a single query. Drivers that do not exist are simply absent
    /// from the result, so the caller can report them.
    /// </summary>
    Task<IReadOnlyList<Driver>> GetDriversByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Admin>> GetAdminsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverAssignmentCandidate>> GetDriverCandidatesAsync(
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        CancellationToken cancellationToken = default);
}