using UserManagement.API.Interfaces;
using UserManagement.API.Models;

namespace UserManagement.API.Services;

public sealed class UserService(TimeProvider time) : IUserService
{
    /// <inheritdoc/>
    public User Create(string countryIsoCode, string locale)
    {
        // Я надеюсь, что для тестового задания достаточно допущения, что guid никогда не повторится(хотя теоретически такое возможно!)
        // + версия 7 к этому устойчивее, насколько я знаю
        var guid = Guid.CreateVersion7();
        // в идеале хранить в DateTimeOffset, но по ТЗ нужен DateTime
        var dateCreated = time.GetUtcNow().UtcDateTime; 

        return new User(guid, countryIsoCode.ToUpperInvariant(), locale, dateCreated, null);
    }

    /// <inheritdoc/>
    public User Update(User user, string countryIsoCode, string locale)
    {
        return user with
        {
            Locale = locale,
            CountryIsoCode = countryIsoCode.ToUpperInvariant(),
            DateModified = time.GetUtcNow().UtcDateTime
        };
    }
}