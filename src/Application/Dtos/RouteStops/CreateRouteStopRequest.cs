namespace Application.Dtos.RouteStops;

public class CreateRouteStopRequest
{
    public Guid ShipmentId { get; init; }
    public string Address { get; init; } = string.Empty;
    public int StopOrder { get; init; }
    public string? Name { get; init; }
}