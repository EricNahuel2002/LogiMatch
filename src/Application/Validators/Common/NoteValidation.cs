using FluentValidation;

namespace Application.Validators.Common;

internal static class NoteValidation
{
    public const int MaxLength = 500;

    public static IRuleBuilderOptions<T, string?> Note<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder.MaximumLength(MaxLength);
    }
}