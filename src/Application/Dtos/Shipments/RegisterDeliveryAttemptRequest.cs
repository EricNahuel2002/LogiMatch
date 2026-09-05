namespace Application.Dtos.Shipments;

public class RegisterDeliveryAttemptRequest
{
    public Guid? RouteStopId { get; init; }
    public bool Succeeded { get; init; }
    public string? Note { get; init; }
    public DateTime? AttemptedAt { get; init; }
}