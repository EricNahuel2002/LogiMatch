using Application.Dtos.Shipments;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class ResumeShipmentValidator : AbstractValidator<ResumeShipmentRequest>
{
    public ResumeShipmentValidator()
    {
        RuleFor(x => x.Note)
            .Note();
    }
}