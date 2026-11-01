using System.Globalization;
using System.Text.Json;
using UserManagement.API.Models;
using UserManagement.API.Utility;

namespace UserManagement.Tests.Infrastructure;

/// <summary>
/// Проверяет, что JSON полученный для <see cref="User"/> валиден и содержит все необходимые поля.
/// Требует наличия в JSON полей из <see cref="ExpectedProperties"/> в camelCase.
/// </summary>
public static class UserJsonValidator
{
    /// <summary>
    /// Требуемые поля, которые валидатор будет требовать в JSON версии <see cref="User"/>
    /// </summary>
    private static readonly string[] ExpectedProperties =
    [
        "userId", // айди пользователя, guid.
        "countryIsoCode", // код страны.
        "locale", // локаль.
        "dateCreated", // время создания в UTC.
        "dateModified" // время в UTC, когда был изменен. Может быть null
    ];

    public static async Task<User> Check(this HttpResponseMessage message, bool isModified)
    {
        var postJson = await message.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(postJson);

        doc.RootElement.CheckElement(isModified);
        var user = doc.RootElement.Deserialize<User>(JsonSerializerOptions.Web);
        user.Should().NotBeNull();

        return user;
    }

    /// <summary>
    /// Проверяет JSON версию User на валидность, наличие необходимых полей и правильный формат времени.
    /// </summary>
    /// <param name="root">JsonElement</param>
    /// <param name="isModified">Был ли пользователь модифицирован? Если да - его время модификации не должно быть нулл.</param>
    private static void CheckElement(this JsonElement root, bool isModified)
    {
        root.ValueKind.Should().Be(JsonValueKind.Object);

        // Проверяем, что в JSON ровно нужные нам свойства и ничего лишнего
        root.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo(ExpectedProperties);

        var userId = GetRequiredString(root, "userId");
        Guid.TryParse(userId, out var guid).Should().BeTrue();
        guid.Should().NotBe(Guid.Empty);
        guid.Should().NotBeEmpty();

        GetRequiredString(root, "countryIsoCode").Should().MatchRegex(RegexPatterns.CountryIsoRegex());
        GetRequiredString(root, "locale").Should().MatchRegex(RegexPatterns.LocaleRegex());

        AssertUtcTimestamp(GetRequiredString(root, "dateCreated"));

        var modified = root.GetProperty("dateModified");
        if (isModified)
            AssertUtcTimestamp(GetRequiredString(root, "dateModified"));
        else
            modified.ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// Получает нужную строку по имени из JSON
    /// </summary>
    /// <param name="root">JsonElement</param>
    /// <param name="name">Имя поля для получения</param>
    /// <returns>Текстовое поле</returns>
    private static string GetRequiredString(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        element.ValueKind.Should().Be(JsonValueKind.String);

        var stringValue = element.GetString();
        stringValue.Should().NotBeNullOrEmpty();
        
        return stringValue;
    }

    /// <summary>
    /// Проверяет, что строка времени в JSON парсится как <see cref="DateTime"/> и находится в UTC формате
    /// </summary>
    /// <param name="value">JSON элемент</param>
    private static void AssertUtcTimestamp(string value)
    {
        value.Should().EndWith("Z"); // отметка часового пояса
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            .Should().BeTrue();
        parsed.Kind.Should().Be(DateTimeKind.Utc);
    }
}