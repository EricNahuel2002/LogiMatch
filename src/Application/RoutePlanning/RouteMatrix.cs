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

    /// <summary>
    /// Narrows the matrix down to the given nodes, in the order they are listed. The result
    /// keeps the same metric and is indexed by position in <paramref name="nodeIndices" />,
    /// so a caller can hand it straight to <see cref="VehicleRoutingProblem" /> with a node
    /// layout that skips whatever it left out.
    /// </summary>
    /// <remarks>
    /// The planner needs this because driver eligibility is decided from the same matrix: a
    /// driver that does not survive the filter is not a problem node, and the matrix the
    /// solver validates against <c>NodeCount</c> has to shrink with it. Re-fetching would cost
    /// a second round trip to the routing provider for data that is already in memory.
    /// </remarks>
    /// <remarks>
    /// <see cref="HasCompleteData" /> is carried over rather than recomputed: a subset of an
    /// incomplete matrix is still incomplete, and a cell outside the projection cannot make
    /// the surviving nodes more trustworthy.
    /// </remarks>
    public RouteMatrix Project(IReadOnlyList<int> nodeIndices)
    {
        ArgumentNullException.ThrowIfNull(nodeIndices);

        if (nodeIndices.Count == 0)
        {
            throw new ArgumentException(
                "A projected matrix needs at least one node.",
                nameof(nodeIndices));
        }

        var ordered = nodeIndices.Order().ToList();

        foreach (var node in nodeIndices)
        {
            if (node < 0 || node >= Size)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nodeIndices),
                    node,
                    $"Node {node} falls outside a matrix of size {Size}.");
            }
        }

        if (!ordered.SequenceEqual(nodeIndices))
        {
            throw new ArgumentException(
                "Projection must preserve the ascending order of the source matrix, otherwise " +
                "the projected indexes no longer line up with the reduced node layout.",
                nameof(nodeIndices));
        }

        if (nodeIndices.Count != nodeIndices.Distinct().Count())
        {
            throw new ArgumentException(
                "A node can only be projected once.",
                nameof(nodeIndices));
        }

        var size = nodeIndices.Count;
        var values = new long[size * size];

        for (var from = 0; from < size; from++)
        {
            for (var to = 0; to < size; to++)
            {
                values[(from * size) + to] = this[nodeIndices[from], nodeIndices[to]];
            }
        }

        return Create(size, values, HasCompleteData);
    }
}
