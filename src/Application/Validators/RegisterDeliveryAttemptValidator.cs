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

        RuleFor(x => x.FailureReason)
            .NotNull()
            .When(x => !x.Succeeded)
            .WithMessage("A failed delivery attempt requires a failure reason.");

        RuleFor(x => x.FailureReason)
            .Null()
            .When(x => x.Succeeded)
            .WithMessage("A successful delivery attempt cannot have a failure reason.");
    }
}