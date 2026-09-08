using Application.Dtos.Drivers;
using FluentValidation;

namespace Application.Validators;

public class UpdateDriverLocationValidator : AbstractValidator<UpdateDriverLocationRequest>
{
    public UpdateDriverLocationValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m);
    }
}