using Domain.ValueObjects;

namespace Application.Integrations;

public interface IGeocodingClient
{
    Task<Coordinate> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}