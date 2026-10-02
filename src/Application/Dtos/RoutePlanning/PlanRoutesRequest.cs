namespace Application.Dtos.RoutePlanning;

/// <summary>
/// Asks for a read-only planning proposal over an explicit set of drivers, vehicles and
/// deposits. The shipments are not chosen by the caller: the planner always works over the
/// pending ones and reports back the ones it had to leave out. Nothing is persisted.
/// </summary>
public class PlanRoutesRequest
{
    public IReadOnlyList<DriverSelectionRequest> DriverSelections { get; init; } = [];
}
