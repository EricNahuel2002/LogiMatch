using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace UnitTests.Application;

internal static class FluentValidationMocks
{
    public static Mock<IValidator<T>> AlwaysValid<T>()
    {
        var mock = new Mock<IValidator<T>>();
        mock.Setup(v => v.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        return mock;
    }
}