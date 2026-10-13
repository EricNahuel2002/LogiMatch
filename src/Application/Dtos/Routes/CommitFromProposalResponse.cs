namespace Application.Dtos.Routes;

public record CommittedRouteDto(
    Guid RouteId,
    Guid DriverId,
    Guid[] RouteStopIds);

public record CommitFromProposalResponse(
    CommittedRouteDto[] Routes);
