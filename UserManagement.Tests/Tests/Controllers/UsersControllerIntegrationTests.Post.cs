using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserManagement.API.DTOs;
using UserManagement.API.Models;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests.Controllers;

public sealed partial class UsersControllerIntegrationTests
{
    /// <summary>
    /// Проверяет, что при отправке валидных данных пользователя сервер возвращает 201 Created,
    /// корректный заголовок Location, сгенерированный UserId, UTC-время создания и CountryIsoCode в верхнем регистре.
    /// </summary>
    [Fact]
    public async Task Create_ValidUser_Returns201CreatedAndValidUser()
    {
        // Arrange - тут нечего делать

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdUser = await response.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();
        createdUser.UserId.Should().NotBeEmpty();
        createdUser.CountryIsoCode.Should().Be("RU");
        createdUser.Locale.Should().Be("ru-RU");
        createdUser.DateCreated.Kind.Should().Be(DateTimeKind.Utc);
        createdUser.DateCreated.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        createdUser.DateModified.Should().BeNull();

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location.ToString().ToLowerInvariant()
            .Should().EndWith($"/api/users/{createdUser.UserId}".ToLowerInvariant());
    }

    /// <summary>
    /// Проверяет, что сервер принимает CountryIsoCode в верхнем, нижнем и смешанном регистрах
    /// и нормализует его к верхнему регистру при сохранении.
    /// </summary>
    [Theory]
    [InlineData("RU")]
    [InlineData("ru")]
    [InlineData("rU")]
    [InlineData("Ru")]
    public async Task Create_CountryIsoCodeWithMixedCase_NormalizesToUppercase(string countryIsoCode)
    {
        // Arrange
        var dto = new UserDto(countryIsoCode, "ru-RU");

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<User>();
        user.Should().NotBeNull();
        user.CountryIsoCode.Should().Be("RU");
    }

    /// <summary>
    /// Проверяет, что сервер игнорирует переданные клиентом в теле JSON поля UserId, DateCreated,
    /// DateModified и неизвестные свойства, генерируя собственные серверные значения.
    /// </summary>
    [Fact]
    public async Task Create_ExtraJsonFields_ServerIgnoresClientValues()
    {
        // Arrange
        var clientGuid = Guid.NewGuid();
        var clientDateCreated = DateTime.UtcNow.AddDays(-30);
        var clientDateModified = DateTime.UtcNow.AddDays(-1);

        var extraFieldsPayload = new
        {
            countryIsoCode = "RU",
            locale = "ru-RU",
            userId = clientGuid,
            dateCreated = clientDateCreated,
            dateModified = clientDateModified,
            unexpectedCustomField = "some-arbitrary-client-data"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", extraFieldsPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<User>();
        user.Should().NotBeNull();
        user.UserId.Should().NotBe(clientGuid);
        user.UserId.Should().NotBeEmpty();
        user.CountryIsoCode.Should().Be("RU");
        user.Locale.Should().Be("ru-RU");
        user.DateCreated.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        user.DateModified.Should().BeNull();
    }

    /// <summary>
    /// Проверяет, что при передаче невалидных данных (Locale или CountryIsoCode)
    /// возвращается 400 Bad Request со структурой ProblemDetails и ошибкой по соответствующему полю.
    /// </summary>
    [Theory]
    // Невалидные CountryIsoCode:
    [InlineData("ru-RU", "11", nameof(UserDto.CountryIsoCode))] // цифры
    [InlineData("ru-RU", "ыы", nameof(UserDto.CountryIsoCode))] // нелатинские символы
    [InlineData("ru-RU", "rrr", nameof(UserDto.CountryIsoCode))] // превышение длины
    [InlineData("ru-RU", "r", nameof(UserDto.CountryIsoCode))] // недостаточная длина
    [InlineData("ru-RU", "RU\n", nameof(UserDto.CountryIsoCode))] // спецсимволы
    [InlineData("ru-RU", "", nameof(UserDto.CountryIsoCode))] // пустая строка
    [InlineData("ru-RU", " ", nameof(UserDto.CountryIsoCode))] // пробел
    [InlineData("ru-RU", " RU", nameof(UserDto.CountryIsoCode))] // пробел в начале
    [InlineData("ru-RU", "RU ", nameof(UserDto.CountryIsoCode))] // пробел в конце
    [InlineData("ru-RU", "R1", nameof(UserDto.CountryIsoCode))] // цифра с буквой
    [InlineData("ru-RU", "1R", nameof(UserDto.CountryIsoCode))] // цифра с буквой
    [InlineData("ru-RU", null, nameof(UserDto.CountryIsoCode))] // null
    // Невалидная Locale:
    [InlineData("11-11", "RU", nameof(UserDto.Locale))] // цифры
    [InlineData("ru-ru", "RU", nameof(UserDto.Locale))] // вторая часть в нижнем регистре
    [InlineData("RU-RU", "RU", nameof(UserDto.Locale))] // первая часть в верхнем регистре
    [InlineData("rU-rU", "RU", nameof(UserDto.Locale))] // смешанный регистр
    [InlineData("ыы-ЫЫ", "RU", nameof(UserDto.Locale))] // нелатинские буквы
    [InlineData("ru-RURU", "RU", nameof(UserDto.Locale))] // превышение длины второй части
    [InlineData("ru", "RU", nameof(UserDto.Locale))] // неполная локаль
    [InlineData("ru_RU", "RU", nameof(UserDto.Locale))] // разделитель подчеркивание
    [InlineData("ru|RU", "RU", nameof(UserDto.Locale))] // разделитель пайп
    [InlineData("ruRU", "RU", nameof(UserDto.Locale))] // без разделителя
    [InlineData("ru-RU\n", "RU", nameof(UserDto.Locale))] // спецсимволы
    [InlineData("", "RU", nameof(UserDto.Locale))] // пустая строка
    [InlineData(" ", "RU", nameof(UserDto.Locale))] // пробел
    [InlineData("r-RU", "RU", nameof(UserDto.Locale))] // длина первой части 1
    [InlineData("rus-RU", "RU", nameof(UserDto.Locale))] // длина первой части 3
    [InlineData("ru-R", "RU", nameof(UserDto.Locale))] // длина второй части 1
    [InlineData("ru-RUS", "RU", nameof(UserDto.Locale))] // длина второй части 3
    [InlineData(null, "RU", nameof(UserDto.Locale))] // null
    public async Task Create_InvalidData_Returns400BadRequest(string? locale, string? countryIsoCode, string expectedErrorProperty)
    {
        // Arrange
        var dto = new UserDto(countryIsoCode!, locale!);

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Errors.Keys.Should().Contain(k => string.Equals(k, expectedErrorProperty, StringComparison.OrdinalIgnoreCase));

        var checkResponse = await _client.GetAsync("/api/users");
        var users = await checkResponse.Content.ReadFromJsonAsync<List<User>>();
        users.Should().BeEmpty();
    }

    /// <summary>
    /// Проверяет, что при одновременной невалидности обоих полей (Locale и CountryIsoCode)
    /// возвращается 400 Bad Request с ошибками по каждому из полей.
    /// </summary>
    [Theory]
    [InlineData("1", "invalid")]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    [InlineData("11", "11-11")]
    [InlineData(null, null)]
    public async Task Create_BothFieldsInvalid_Returns400WithBothErrors(string? countryIsoCode, string? locale)
    {
        // Arrange
        var dto = new UserDto(countryIsoCode!, locale!);

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Errors.Keys.Should().Contain(k => string.Equals(k, nameof(UserDto.CountryIsoCode), StringComparison.OrdinalIgnoreCase));
        problemDetails.Errors.Keys.Should().Contain(k => string.Equals(k, nameof(UserDto.Locale), StringComparison.OrdinalIgnoreCase));

        var checkResponse = await _client.GetAsync("/api/users");
        var users = await checkResponse.Content.ReadFromJsonAsync<List<User>>();
        users.Should().BeEmpty();
    }
}
