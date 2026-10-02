using Domain.Entities;

namespace Application.RoutePlanning;

/// <summary>
/// Decides which deposit each planned driver starts from and returns to.
/// </summary>
/// <remarks>
/// This runs before the routing matrix is requested, not as part of the solver, because every
/// node in the problem that is neither a driver start nor a vehicle end is mandatory: leaving an
/// unassigned deposit in the layout would make the whole model infeasible. Only the deposits this
/// policy returns ever enter the problem, so it must see the whole catalogue and settle the
/// mapping first.
/// </remarks>
public interface IDepositAssignmentPolicy
{
    /// <summary>
    /// Maps every driver to the deposit it will use.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The catalogue has no active deposit, so a driver left to the planner has nowhere to
    /// start from or return to.
    /// </exception>
    IReadOnlyDictionary<Guid, Guid> Assign(
        IReadOnlyList<DepositAssignment> drivers,
        IReadOnlyList<Deposit> deposits);
}