using UserManagement.API.DTOs;

namespace UserManagement.Tests.Infrastructure;

/// <summary>
/// Класс с различными константами для тестов.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Стандартное смещение по времени для тестов, удобно сохраненное в константу.
    /// </summary>
    public static readonly DateTimeOffset DefaultTimeOffset 
        = new (2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Стандартный валидный ДТО для создания пользователя.
    /// </summary>
    public static readonly UserDto DefaultValidUserDto = new UserDto("RU", "ru-RU");
}