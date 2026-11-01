using UserManagement.API.Models;
using UserManagement.API.Validators;

namespace UserManagement.API.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Создает пользователя с заданным <see cref="User.CountryIsoCode"/> и <see cref="User.Locale"/>.
    /// </summary>
    /// <param name="countryIsoCode">Код страны пользователя. Обязан быть валидным по правилам <see cref="UserValidator"/></param>
    /// <param name="locale">Локаль пользователя. Обязана быть валидной по правилам <see cref="UserValidator"/></param>
    /// <returns>Созданного нового пользователя</returns>
    public User Create(string countryIsoCode, string locale);

    /// <summary>
    /// Обновляет у переданного пользователя его <see cref="User.CountryIsoCode"/> и <see cref="User.Locale"/>
    /// </summary>
    /// <param name="user">Пользователь для обновления</param>
    /// <param name="countryIsoCode">Код страны пользователя. Обязан быть валидным по правилам <see cref="UserValidator"/></param>
    /// <param name="locale">Локаль пользователя. Обязана быть валидной по правилам <see cref="UserValidator"/></param>
    /// <returns>Измененного пользователя</returns>
    public User Update(User user, string countryIsoCode, string locale);
}