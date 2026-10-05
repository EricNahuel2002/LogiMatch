using System.ComponentModel.DataAnnotations;
using Domain.Services;

namespace Application.RoutePlanning;

/// <summary>
/// Knobs for how <see cref="Domain.Services.DriverScoring" /> feeds route planning: which
/// drivers survive the eligibility filter, and how hard the solver prefers the better ones.
/// </summary>
/// <remarks>
/// Lives in Application rather than Infrastructure because both layers read it: the eligibility
/// filter needs the thresholds, the solver needs the penalty magnitudes. Configuration binds it.
/// </remarks>
public sealed class DriverScoringOptions
{
    /// <summary>
    /// Absolute score floor on the 0..1 scale produced by <see cref="DriverScoring.Rank" />.
    /// </summary>
    /// <remarks>
    /// The score is a weighted min-max normalization over the pool being planned, so its
    /// absolute meaning is weak. The default sits at the lower quartile of the score
    /// distribution and matches <c>ShipmentPriorityScoring.LowThreshold</c>: it drops drivers
    /// in the bottom half of the pool without pretending to know what a "good" score is
    /// outside it.
    /// </remarks>
    [Range(typeof(decimal), "0", "1")]
    public decimal MinimumScore { get; set; } = 0.25m;

    /// <summary>
    /// How far below the best available driver a driver may score and still be planned with.
    /// </summary>
    /// <remarks>
    /// Relative on purpose. A tolerance measured against a perfect score would behave
    /// differently depending on how strong the pool happens to be. The default is wide enough
    /// to stay inert for a normal pool and only bites on a driver scoring less than half of
    /// the best one.
    /// </remarks>
    [Range(typeof(decimal), "0", "1")]
    public decimal ScoreTolerance { get; set; } = 0.5m;

    /// <summary>
    /// Number of drivers kept when the thresholds would otherwise leave fewer, so a preview is
    /// still possible. Does not resurrect a driver rejected for insufficient capacity.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinimumDrivers { get; set; } = 1;

    /// <summary>
    /// Meters of arc cost added when an urgent shipment is served by the worst driver in the
    /// plan. Read as "pairing this shipment with this driver costs as much as driving this many
    /// extra meters", which is what makes it interpretable and tunable.
    /// </summary>
    /// <remarks>
    /// Applied only to shipment nodes, scaled by the shipment urgency. A driver scoring 1.0
    /// pays no penalty at all, so the term cannot distort a plan built on the best drivers.
    /// </remarks>
    [Range(typeof(decimal), "0", "1000000")]
    public decimal MismatchPenaltyMeters { get; set; } = 3000m;

    /// <summary>
    /// Meters of fixed cost each driver pays for being used, scaled down by their score.
    /// Discourages spreading work over many mediocre drivers when a few good ones can absorb it.
    /// </summary>
    [Range(typeof(decimal), "0", "1000000")]
    public decimal FixedCostMeters { get; set; } = 5000m;

    public DriverEligibilityPolicy ToPolicy() =>
        new(MinimumScore, ScoreTolerance, MinimumDrivers);
}