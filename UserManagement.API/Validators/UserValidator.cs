using FluentValidation;
using UserManagement.API.DTOs;
using UserManagement.API.Utility;

namespace UserManagement.API.Validators;

public sealed class UserValidator : AbstractValidator<UserDto>
{
    public UserValidator()
    {
        RuleFor(x => x.CountryIsoCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Код страны не должен быть пустым")
            .Matches(RegexPatterns.CountryIsoRegex()).WithMessage("Код страны должен состоять из двух латинских букв");
        
        RuleFor(x => x.Locale)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Локаль не должна быть пустой")
            .Matches(RegexPatterns.LocaleRegex()).WithMessage("Локаль должна быть в формате типа ru-RU");
    }
}