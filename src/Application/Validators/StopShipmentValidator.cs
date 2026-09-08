using Application.Dtos.Shipments;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class StopShipmentValidator : AbstractValidator<StopShipmentRequest>
{
    public StopShipmentValidator()
    {
        RuleFor(x => x.Note)
            .Note();
    }
}