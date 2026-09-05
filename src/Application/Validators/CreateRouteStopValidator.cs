using Application.Dtos.RouteStops;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class CreateRouteStopValidator : AbstractValidator<CreateRouteStopRequest>
{
    public CreateRouteStopValidator()
    {
        RuleFor(x => x.ShipmentId)
            .NotEmpty();

        RuleFor(x => x.StopOrder)
            .GreaterThan(0);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m);

        RuleFor(x => x.Name)
            .Note();
    }
}