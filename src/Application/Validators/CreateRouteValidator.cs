using Application.Dtos.Routes;
using FluentValidation;

namespace Application.Validators;

public class CreateRouteValidator : AbstractValidator<CreateRouteRequest>
{
    public CreateRouteValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m);
    }
}