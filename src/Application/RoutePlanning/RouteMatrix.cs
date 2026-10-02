namespace Application.RoutePlanning;

/// <summary>
/// Square dense matrix of a single metric between all pairs of planning locations.
/// Row and column order must match the order of the locations passed to
/// <see cref="Integrations.IRouteMatrixClient.GetMatrixAsync" />.
/// </summary>
public sealed class RouteMatrix
{
    /// <summary>
    /// Value used for cells that could not be resolved. It is large enough to keep the
    /// routing solver from ever selecting the arc, without overflowing when summed.
    /// </summary>
    public const long UnreachableValue = long.MaxValue / 4;

    private readonly long[] _values;

    private RouteMatrix(int size, long[] values, bool hasCompleteData)
    {
        Size = size;
        _values = values;
        HasCompleteData = hasCompleteData;
    }

    /// <summary>
    /// Number of locations on each side of the matrix.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// False when at least one cell could not be resolved and was filled with
    /// <see cref="UnreachableValue" />. A plan built on such a matrix is not trustworthy.
    /// </summary>
    public bool HasCompleteData { get; }

    public long this[int from, int to] => _values[(from * Size) + to];

    /// <summary>
    /// Creates a matrix from row-major values. <paramref name="values" /> length must be
    /// exactly <paramref name="size" /> squared.
    /// </summary>
    public static RouteMatrix Create(int size, long[] values, bool hasCompleteData = true)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "The matrix size must be greater than zero.");
        }

        if (values.Length != size * size)
        {
            throw new ArgumentException(
                $"A {size}x{size} matrix requires {size * size} values.",
                nameof(values));
        }

        return new RouteMatrix(size, values, hasCompleteData);
    }
}
