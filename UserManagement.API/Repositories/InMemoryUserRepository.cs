using System.Collections.Concurrent;
using UserManagement.API.Interfaces;
using UserManagement.API.Models;

namespace UserManagement.API.Repositories;

/// <summary>
/// Класс отвечающий за многопоточное хранение, поиск и обновление пользователей.
/// Хранит все данные в формате <see cref="KeyValuePair"/> внутри оперативной памяти через <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    /// <summary>
    /// Имитация базы данных.
    /// <list type="table">
    ///   <item>
    ///     <term>Ключ</term>
    ///     <description>Айди пользователя формата <see cref="Guid"/> соответствующий <see cref="User.UserId"/></description>
    ///   </item>
    ///   <item>
    ///     <term>Значение</term>
    ///     <description>Класс хранящий данные пользователя</description>
    ///   </item>
    /// </list>
    /// </summary>
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    /// <inheritdoc/>
    /// <remarks>
    /// Если пользователь уже существует в хранилище - просто возвращает его, не добавляя дубликат.
    /// </remarks>
    public User Add(User user)
    {
        // Я не уверен, что по ТЗ требуется сделать в случае, если пользователь уже существует.
        // Но раз контракт обязан возвращать User, я решил что пусть просто возвращает существующего пользователя.
        // Хотя тогда правильнее было бы назвать его GetOrAddUser.
        return _users.GetOrAdd(user.UserId, user);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Если пользователя попытаются записать сразу несколько потоков - реально пользователя обновит только последний.
    /// </remarks>
    public bool Update(User user)
    {
        // Крутимся в цикле до тех пор, пока либо успешно не обновим,
        // либо не убедимся, что пользователя вообще нет в словаре.
        // Это защита для race condition, чтобы метод мог работать хоть с 99999999 потоками.
        // Но запишет данные все равно только последний поток.
        while (_users.TryGetValue(user.UserId, out var existingUser))
        {
            if (_users.TryUpdate(user.UserId, user, existingUser))
                return true; // Успешно обновили
        
            // Если TryUpdate вернул false - значит, другой поток
            // успел обновить пользователя между TryGetValue и TryUpdate.
            // Цикл пойдет на следующую итерацию, прочитает уже свежую версию и попробует снова.
        }

        // Если TryGetValue вернул false - пользователя действительно нет.
        return false;
    }

    /// <inheritdoc/>
    public User? GetById(Guid userId)
    {
        return _users.GetValueOrDefault(userId);
    }

    /// <inheritdoc/> 
    public IEnumerable<User> GetAll()
    {
        return _users.Values;
    }
}