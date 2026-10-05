namespace Domain.Enums;

/// <summary>
/// Why a driver considered for a plan did not make it into the routing problem.
/// </summary>
public enum DriverIneligibilityReason
{
    /// <summary>
    /// The vehicle could not carry the heaviest shipment of the plan, so no assignment could
    /// satisfy capacity. Decided before scoring, never revisited.
    /// </summary>
    InsufficientVehicleCapacity = 0,

    /// <summary>
    /// Scored below the configured floor.
    /// </summary>
    ScoreBelowMinimum = 1,

    /// <summary>
    /// Scored far enough below the best available driver to be considered unusable.
    /// </summary>
    ScoreBelowTolerance = 2
}