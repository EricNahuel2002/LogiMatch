namespace IntegrationTests.Infrastructure;

/// <summary>
/// Freezes the application clock so time-dependent behavior, such as the route planning
/// horizon, is reproducible and independent of the machine running the suite.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
