namespace Application.RoutePlanning;

public interface IVehicleRoutingSolver
{
    /// <summary>
    /// Assigns every shipment in <paramref name="problem" /> to exactly one driver route.
    /// Returns null when the constraints cannot be satisfied at all, which the caller must
    /// surface as an explicit failure rather than as a partial plan.
    /// </summary>
    VehicleRoutingSolution? Solve(VehicleRoutingProblem problem, CancellationToken cancellationToken = default);
}
