using Application.Dtos.Orders;
using FluentValidation;

namespace Application.Validators;

public class AssignOrderDriverValidator : AbstractValidator<AssignOrderDriverRequest>
{
    public AssignOrderDriverValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEqual(Guid.Empty)
            .When(x => x.DriverId.HasValue)
            .WithMessage("Driver id cannot be empty when provided.");
    }
}