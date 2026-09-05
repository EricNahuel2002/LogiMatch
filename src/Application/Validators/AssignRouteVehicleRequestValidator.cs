using Application.Dtos.Routes;
using FluentValidation;

namespace Application.Validators;

public class AssignRouteVehicleRequestValidator : AbstractValidator<AssignRouteVehicleRequest>
{
    public AssignRouteVehicleRequestValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEqual(Guid.Empty);
    }
}