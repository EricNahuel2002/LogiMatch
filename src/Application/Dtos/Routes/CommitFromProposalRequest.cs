namespace Application.Dtos.Routes;

public record CommitProposalStopRequest(
    Guid ShipmentId,
    int StopOrder);

public record CommitProposalAssignmentRequest(
    Guid DriverId,
    Guid VehicleId,
    Guid DepositId,
    CommitProposalStopRequest[] Stops);

public record CommitFromProposalRequest(
    CommitProposalAssignmentRequest[] Assignments);
