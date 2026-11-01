using UserManagement.API.Models;

namespace UserManagement.API.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Добавляет пользователя в хранилище и возвращает его назад.
    /// </summary>
    /// <param name="user">Пользователь для добавления</param>
    /// <returns>Пользователя из хранилища</returns>
    public User Add(User user);

    /// <summary>
    /// Полностью обновляет запись о пользователе в хранилище на переданную.
    /// Используется <see cref="User.UserId"/> для поиска существующего пользователя для замены.
    /// </summary>
    /// <param name="user">Пользователь, который заменит собой существующего.</param>
    /// <returns>
    /// Удалась ли замена или нет.
    /// <list type="table">
    ///   <listheader>
    ///     <term>Значение</term>
    ///     <description>Описание действия</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see langword="false"/></term>
    ///     <description>Пользователь с таким <see cref="Guid"/> не существует в базе, замены не было</description>
    ///   </item>
    ///   <item>
    ///     <term><see langword="true"/></term>
    ///     <description>Пользователь успешно найден и обновлен</description>
    ///   </item>
    /// </list>
    /// </returns>
    public bool Update(User user);

    /// <summary>
    /// Ищет пользователя по его <see cref="Guid"/>.
    /// Возвращает найденного пользователя
    /// </summary>
    /// <param name="userId">Айди пользователя соответствующий <see cref="User.UserId"/></param>
    /// <returns>Найденный пользователь/null если пользователя не существует</returns>
    public User? GetById(Guid userId);

    /// <summary>
    /// Возвращает всех пользователей существующих в базе данных.
    /// </summary>
    /// <returns>Все пользователи хранящиеся в базе данных</returns>
    public IEnumerable<User> GetAll();
}