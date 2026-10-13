namespace Application.Dtos.Shipments;

public record DeliveryAttemptDto(
    Guid Id,
    Guid ShipmentId,
    Guid OrderId,
    Guid? RouteStopId,
    int AttemptNumber,
    DateTime AttemptedAt,
    bool Succeeded,
    Domain.Enums.DeliveryFailureReason? FailureReason,
    string? Note);
