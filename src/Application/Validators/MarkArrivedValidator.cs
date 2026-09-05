using Application.Dtos.Shipments;
using Application.Validators.Common;
using FluentValidation;

namespace Application.Validators;

public class MarkArrivedValidator : AbstractValidator<MarkArrivedRequest>
{
    public MarkArrivedValidator()
    {
        RuleFor(x => x.Note)
            .Note();
    }
}