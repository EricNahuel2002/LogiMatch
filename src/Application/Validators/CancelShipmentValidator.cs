using Application.Dtos.Shipments;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class CancelShipmentValidator : AbstractValidator<CancelShipmentRequest>
{
    public CancelShipmentValidator()
    {
        RuleFor(x => x.Note)
            .Note();
    }
}