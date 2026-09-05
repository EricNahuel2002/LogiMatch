using Application.Dtos.Routes;
using FluentValidation;

namespace Application.Validators;

public class AssignRouteDriverRequestValidator : AbstractValidator<AssignRouteDriverRequest>
{
    public AssignRouteDriverRequestValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEqual(Guid.Empty);
    }
}