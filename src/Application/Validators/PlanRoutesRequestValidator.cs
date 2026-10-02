using Application.Dtos.RoutePlanning;
using FluentValidation;

namespace Application.Validators;

public class PlanRoutesRequestValidator : AbstractValidator<PlanRoutesRequest>
{
    public PlanRoutesRequestValidator()
    {
        RuleFor(x => x.DriverSelections)
            .NotEmpty()
            .Must(selections => selections.Select(s => s.DriverId).Distinct().Count()
                == selections.Count)
            .WithMessage("Each driver can only be selected once.")
            .Must(selections => selections.Select(s => s.VehicleId).Distinct().Count()
                == selections.Count)
            .WithMessage("Each vehicle can only be assigned to one driver.");

        RuleForEach(x => x.DriverSelections).ChildRules(selection =>
        {
            selection.RuleFor(s => s.DriverId).NotEmpty();
            selection.RuleFor(s => s.VehicleId).NotEmpty();

            // An explicit deposit may be overridden or omitted, but Guid.Empty is never a
            // valid choice: it would read as "unknown deposit" instead of "let the planner
            // decide" and turn into a 404 on a field the caller left out on purpose.
            selection.RuleFor(s => s.DepositId)
                .NotEqual(Guid.Empty)
                .WithMessage("The deposit must be a real deposit id or omitted.");
        });
    }
}
