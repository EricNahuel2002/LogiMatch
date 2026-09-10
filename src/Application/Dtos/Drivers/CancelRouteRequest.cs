using Domain.Enums;

namespace Application.Dtos.Drivers;

public class CancelRouteRequest
{
    public RouteCancellationReason Reason { get; init; }

    public string? Note { get; init; }
}