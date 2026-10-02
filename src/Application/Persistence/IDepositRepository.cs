using Domain.Entities;

namespace Application.Persistence;

public interface IDepositRepository
{
    /// <summary>
    /// Every deposit in the catalogue, active or not. The planner needs the whole set to pick
    /// the nearest active one and to tell an inactive deposit apart from a missing one.
    /// </summary>
    Task<IReadOnlyList<Deposit>> GetAllAsync(
        CancellationToken cancellationToken = default);
}