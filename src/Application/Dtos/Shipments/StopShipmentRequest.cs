namespace Application.Dtos.Shipments;

public class StopShipmentRequest
{
    public Guid? RouteStopId { get; init; }
    public string? Note { get; init; }
}