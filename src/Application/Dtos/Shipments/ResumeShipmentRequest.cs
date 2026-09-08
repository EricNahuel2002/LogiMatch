namespace Application.Dtos.Shipments;

public class ResumeShipmentRequest
{
    public Guid? RouteStopId { get; init; }
    public string? Note { get; init; }
}