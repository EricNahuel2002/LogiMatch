using Application.Dtos.Shipments;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class RegisterDeliveryAttemptValidator : AbstractValidator<RegisterDeliveryAttemptRequest>
{
    public RegisterDeliveryAttemptValidator()
    {
        RuleFor(x => x.Note)
            .Note();

        RuleFor(x => x.RouteStopId)
            .NotEqual(Guid.Empty)
            .When(x => x.RouteStopId.HasValue)
            .WithMessage("Route stop id cannot be empty when provided.");
    }
}