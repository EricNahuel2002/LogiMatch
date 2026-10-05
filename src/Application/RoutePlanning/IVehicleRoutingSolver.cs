namespace Application.RoutePlanning;

public interface IVehicleRoutingSolver
{
    /// <summary>
    /// Assigns every shipment in <paramref name="problem" /> to exactly one driver route.
    /// A missing <see cref="RoutingSolveOutcome.Solution" /> is always an explicit failure the
    /// caller must surface, never a partial plan.
    /// </summary>
    RoutingSolveOutcome Solve(VehicleRoutingProblem problem, CancellationToken cancellationToken = default);
}

/// <summary>
/// Why the solver came back without a plan.
/// </summary>
/// <remarks>
/// The distinction that matters to the caller is between a plan that was proven impossible and a
/// search that simply ran out of time: the first one is a fact about the data, the second one is a
/// fact about the solver, and they lead to opposite operator reactions.
/// </remarks>
public enum RoutingSolveFailure
{
    /// <summary>A solution was found; <see cref="RoutingSolveOutcome.Failure" /> carries no meaning.</summary>
    None,

    /// <summary>
    /// The search ended without a solution because it hit its time limit. The plan may well be
    /// feasible.
    /// </summary>
    NoSolutionWithinTimeLimit,

    /// <summary>The search proved that no assignment satisfies the constraints.</summary>
    Infeasible,

    /// <summary>
    /// The solver rejected the model itself, which means the problem was built wrong rather than
    /// being unplannable.
    /// </summary>
    InvalidModel
}

/// <summary>
/// The result of a solve attempt: either a complete plan or the reason there is none.
/// </summary>
/// <param name="Solution">The complete plan, or <c>null</c> when <paramref name="Failure" /> is set.</param>
/// <param name="Failure">Why there is no plan. <see cref="RoutingSolveFailure.None" /> when there is one.</param>
public sealed record RoutingSolveOutcome(VehicleRoutingSolution? Solution, RoutingSolveFailure Failure)
{
    public static RoutingSolveOutcome Solved(VehicleRoutingSolution solution) => new(solution, RoutingSolveFailure.None);

    public static RoutingSolveOutcome Failed(RoutingSolveFailure failure) => new(null, failure);
}