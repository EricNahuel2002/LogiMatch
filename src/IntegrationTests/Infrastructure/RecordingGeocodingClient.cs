using Application.Integrations;
using Domain.ValueObjects;

namespace IntegrationTests.Infrastructure;

public sealed class RecordingGeocodingClient : IGeocodingClient
{
    public Coordinate Coordinate { get; set; } = new(-34.6083m, -58.3816m);

    public Task<Coordinate> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Coordinate);
    }
}