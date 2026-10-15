using Application.Dtos.Routes;
using FluentValidation;

namespace Application.Validators;

public class CommitFromProposalRequestValidator : AbstractValidator<CommitFromProposalRequest>
{
    public CommitFromProposalRequestValidator()
    {
        RuleFor(x => x.Assignments)
            .NotEmpty()
            .Must(assignments => assignments.Select(a => a.DriverId).Distinct().Count()
                == assignments.Length)
            .WithMessage("Each driver can only receive one route per commit.")
            .Must(assignments => assignments.Select(a => a.VehicleId).Distinct().Count()
                == assignments.Length)
            .WithMessage("Each vehicle can only be used by one route per commit.")
            .Must(assignments => assignments
                .SelectMany(a => a.Stops)
                .Select(s => s.ShipmentId)
                .Distinct()
                .Count() == assignments.Sum(a => a.Stops.Length))
            .WithMessage("Each shipment can only appear once in the proposal.");

        RuleForEach(x => x.Assignments).ChildRules(assignment =>
        {
            assignment.RuleFor(a => a.DriverId).NotEmpty();
            assignment.RuleFor(a => a.VehicleId).NotEmpty();
            assignment.RuleFor(a => a.DepositId).NotEmpty();
            assignment.RuleFor(a => a.Stops).NotEmpty();

            assignment.RuleFor(a => a.Stops)
                .Must(stops => stops.Select(s => s.StopOrder).Distinct().Count() == stops.Length)
                .WithMessage("Stops within a route must have distinct orders.")
                .When(a => a.Stops is not null);

            assignment.RuleForEach(a => a.Stops).ChildRules(stop =>
            {
                stop.RuleFor(s => s.ShipmentId).NotEmpty();
                stop.RuleFor(s => s.StopOrder).GreaterThan(0);
            });
        });
    }
}
