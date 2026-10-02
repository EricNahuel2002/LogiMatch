namespace Application.RoutePlanning;

/// <summary>
/// A time window expressed in seconds elapsed since the problem horizon start, the same
/// unit the duration matrix uses.
/// </summary>
public readonly record struct TimeWindow(long StartSeconds, long EndSeconds)
{
    public bool Contains(long seconds) => seconds >= StartSeconds && seconds <= EndSeconds;
}
