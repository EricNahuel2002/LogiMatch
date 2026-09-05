namespace Application.Dtos.RouteStops;

public class CreateRouteStopRequest
{
    public Guid ShipmentId { get; init; }
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public int StopOrder { get; init; }
    public string? Name { get; init; }
}