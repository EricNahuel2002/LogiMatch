using Application.Dtos.RouteStops;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class CreateRouteStopValidator : AbstractValidator<CreateRouteStopRequest>
{
    public const int MaxAddressLength = 200;

    public CreateRouteStopValidator()
    {
        RuleFor(x => x.ShipmentId)
            .NotEmpty();

        RuleFor(x => x.StopOrder)
            .GreaterThan(0);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(MaxAddressLength);

        RuleFor(x => x.Name)
            .Note();
    }
}