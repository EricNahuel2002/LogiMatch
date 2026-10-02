using Application.Integrations;
using Application.RoutePlanning;
using Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

/// <summary>
/// Resolves the full NxN distance and duration matrix between planning locations.
/// </summary>
/// <remarks>
/// OpenRouteService caps a matrix request at 3500 origin x destination pairs, so locations
/// are split into blocks of at most <see cref="MaxLocationsPerBlock" />. Only the upper
/// triangle of blocks is requested: driving distance and duration are symmetric for a given
/// profile, so the lower triangle is filled by transposing. That makes the request count
/// (blocks * (blocks + 1)) / 2 instead of blocks squared, and it is never one request per pair.
/// This holds for the driving profiles used here, where the fastest path in each direction
/// has the same cost; a profile with asymmetric one-way penalties would need the full triangle.
/// </remarks>
public sealed class OpenRouteServiceFullMatrixClient : IRouteMatrixClient
{
    /// <summary>
    /// 50 x 50 = 2500 cells, below the 3500 pair limit.
    /// </summary>
    public const int MaxLocationsPerBlock = 50;

    private readonly OpenRouteServiceMatrixGateway _gateway;

    public OpenRouteServiceFullMatrixClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenRouteServiceFullMatrixClient> logger)
    {
        _gateway = new OpenRouteServiceMatrixGateway(httpClient, configuration, logger);
    }

    public async Task<RouteMatrixSet> GetMatrixAsync(
        IReadOnlyList<Coordinate> locations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(locations);

        if (locations.Count == 0)
        {
            throw new ArgumentException("At least one location is required.", nameof(locations));
        }

        _gateway.EnsureConfigured();

        var size = locations.Count;
        var distances = new long[size * size];
        var durations = new long[size * size];
        var hasCompleteData = true;

        for (var i = 0; i < size; i++)
        {
            distances[(i * size) + i] = 0;
            durations[(i * size) + i] = 0;
        }

        var payload = locations
            .Select(l => new[] { l.Longitude, l.Latitude })
            .ToArray();

        var blocks = Enumerable.Range(0, size)
            .Chunk(MaxLocationsPerBlock)
            .Select(block => block.ToArray())
            .ToList();

        for (var blockRow = 0; blockRow < blocks.Count; blockRow++)
        {
            for (var blockColumn = blockRow; blockColumn < blocks.Count; blockColumn++)
            {
                var block = await _gateway.GetBlockAsync(
                    payload,
                    blocks[blockRow],
                    blocks[blockColumn],
                    cancellationToken);

                for (var row = 0; row < blocks[blockRow].Length; row++)
                {
                    var from = blocks[blockRow][row];

                    for (var column = 0; column < blocks[blockColumn].Length; column++)
                    {
                        var to = blocks[blockColumn][column];

                        var distance = ReadCell(block.Distances, row, column);
                        var duration = ReadCell(block.Durations, row, column);

                        if (!distance.HasValue || !duration.HasValue)
                        {
                            hasCompleteData = false;
                        }

                        distances[(from * size) + to] = ToMatrixValue(distance);
                        durations[(from * size) + to] = ToMatrixValue(duration);

                        if (from != to)
                        {
                            distances[(to * size) + from] = ToMatrixValue(distance);
                            durations[(to * size) + from] = ToMatrixValue(duration);
                        }
                    }
                }
            }
        }

        return new RouteMatrixSet(
            RouteMatrix.Create(size, distances, hasCompleteData),
            RouteMatrix.Create(size, durations, hasCompleteData));
    }

    private static long ToMatrixValue(double? value) => value is { } resolved
        ? (long)Math.Round(resolved)
        : RouteMatrix.UnreachableValue;

    private static double? ReadCell(double?[][]? block, int row, int column)
    {
        if (block is null || block.Length <= row)
        {
            return null;
        }

        var values = block[row];
        return values is null || values.Length <= column ? null : values[column];
    }
}
