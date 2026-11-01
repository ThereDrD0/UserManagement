using FluentValidation;

namespace UserManagement.API.Validators;

public sealed class StringGuidValidator : AbstractValidator<string>
{
    public StringGuidValidator()
    {
        RuleFor(x => x)
            .Must(x => Guid.TryParse(x, out var guid) && guid != Guid.Empty)
            .WithMessage("Значение должно быть корректным непустым GUID.");
    }
}