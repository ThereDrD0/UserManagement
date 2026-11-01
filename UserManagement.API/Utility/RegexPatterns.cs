using System.Text.RegularExpressions;
using UserManagement.API.Models;

namespace UserManagement.API.Utility;

/// <summary>
/// Класс-хелпер с регексами в оптимизированной форме.
/// </summary>
public static partial class RegexPatterns
{
    /// <summary>
    /// Регекс для проверки <see cref="User.CountryIsoCode"/> на валидность.
    /// Проверяет, что в коде содержатся только 2 латинские буквы
    /// </summary>
    /// <remarks>
    /// Важно!! Не захватывает требование на верхний регистр
    /// </remarks>
    [GeneratedRegex(@"\A[a-zA-Z]{2}\z", RegexOptions.CultureInvariant, 100)]
    public static partial Regex CountryIsoRegex();
    
    /// <summary>
    /// Регекс для проверки <see cref="User.Locale"/> на валидность.
    /// Проверяет, что локаль в правильном формате типа ru-RU, где 2 латинские буквы в нижнем регистре, тире,
    /// а потом еще 2 латинские буквы в верхнем регистре.
    /// </summary>
    [GeneratedRegex(@"\A[a-z]{2}-[A-Z]{2}\z", RegexOptions.CultureInvariant, 100)]
    public static partial Regex LocaleRegex();
}