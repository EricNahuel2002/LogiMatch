using Application.Dtos.Drivers;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class CancelRouteRequestValidator : AbstractValidator<CancelRouteRequest>
{
    public CancelRouteRequestValidator()
    {
        RuleFor(x => x.Reason)
            .IsInEnum();

        RuleFor(x => x.Note)
            .Note();
    }
}