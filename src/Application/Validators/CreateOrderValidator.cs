using Application.Dtos.Orders;
using FluentValidation;

namespace Application.Validators;

public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty();

        RuleFor(x => x.CreatedByAdminId)
            .NotEmpty();

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("An order requires at least one item.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateOrderItemValidator());

        RuleFor(x => x.DeliveryWindowEndAt)
            .NotNull()
            .WithMessage("A delivery window requires both start and end.")
            .When(x => x.DeliveryWindowStartAt.HasValue);

        RuleFor(x => x.DeliveryWindowStartAt)
            .NotNull()
            .WithMessage("A delivery window requires both start and end.")
            .When(x => x.DeliveryWindowEndAt.HasValue);

        RuleFor(x => x.DeliveryWindowEndAt)
            .Must((request, end) =>
                end.HasValue
                && request.DeliveryWindowStartAt.HasValue
                && end > request.DeliveryWindowStartAt)
            .WithMessage("The delivery window end must be after the start.")
            .When(x => x.DeliveryWindowStartAt.HasValue && x.DeliveryWindowEndAt.HasValue);
    }
}