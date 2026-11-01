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
    /// Проверяет успешное обновление данных пользователя через PUT /api/users/{id},
    /// фиксацию DateModified и сохранение неизменности UserId и DateCreated.
    /// </summary>
    [Fact]
    public async Task Update_ExistingUser_Returns200OkWithUpdatedData()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        var updateDto = new UserDto("US", "en-US");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser.UserId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedUser = await response.Content.ReadFromJsonAsync<User>();
        updatedUser.Should().NotBeNull();
        updatedUser.UserId.Should().Be(createdUser.UserId);
        updatedUser.CountryIsoCode.Should().Be("US");
        updatedUser.Locale.Should().Be("en-US");
        updatedUser.DateCreated.Should().Be(createdUser.DateCreated);
        updatedUser.DateModified.Should().NotBeNull();
        updatedUser.DateModified.Should().Be(createdUser.DateCreated.AddSeconds(5));
        updatedUser.DateModified!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    /// <summary>
    /// Проверяет успешное обновление пользователя при передаче GUID в верхнем регистре (hex A-F) в URL.
    /// </summary>
    [Fact]
    public async Task Update_GuidInUppercase_Returns200Ok()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        var updateDto = new UserDto("US", "en-US");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser.UserId.ToString().ToUpperInvariant()}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedUser = await response.Content.ReadFromJsonAsync<User>();
        updatedUser.Should().NotBeNull();
        updatedUser.UserId.Should().Be(createdUser.UserId);
        updatedUser.CountryIsoCode.Should().Be("US");
        updatedUser.Locale.Should().Be("en-US");
    }

    /// <summary>
    /// Проверяет, что при обновлении код страны нормализуется к верхнему регистру при любом исходном регистре.
    /// </summary>
    [Theory]
    [InlineData("KZ")]
    [InlineData("kz")]
    [InlineData("kZ")]
    [InlineData("Kz")]
    public async Task Update_CountryIsoCodeWithMixedCase_NormalizesToUppercase(string countryIsoCode)
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/users", new UserDto("RU", "ru-RU"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        var updateDto = new UserDto(countryIsoCode, "kk-KZ");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser!.UserId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedUser = await response.Content.ReadFromJsonAsync<User>();
        updatedUser.Should().NotBeNull();
        updatedUser.UserId.Should().Be(createdUser.UserId);
        updatedUser.CountryIsoCode.Should().Be("KZ");
    }

    /// <summary>
    /// Проверяет обновление только кода страны при сохранении неизменным прежнего значения локали
    /// согласно п. 119 test-cases.md: CountryIsoCode обновлен, Locale сохранена, DateModified зафиксирован.
    /// </summary>
    [Fact]
    public async Task Update_OnlyCountryIsoCode_MaintainsExistingLocale()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        var updateDto = new UserDto("US", createdUser.Locale);

        // Act
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser.UserId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedUser = await response.Content.ReadFromJsonAsync<User>();
        updatedUser.Should().NotBeNull();
        updatedUser.UserId.Should().Be(createdUser.UserId);
        updatedUser.CountryIsoCode.Should().Be("US");
        updatedUser.Locale.Should().Be("ru-RU");
        updatedUser.DateCreated.Should().Be(createdUser.DateCreated);
        updatedUser.DateModified.Should().NotBeNull();
        updatedUser.DateModified.Should().Be(createdUser.DateCreated.AddSeconds(5));
        updatedUser.DateModified!.Value.Kind.Should().Be(DateTimeKind.Utc);

        var getResponse = await _client.GetAsync($"/api/users/{createdUser.UserId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetchedUser = await getResponse.Content.ReadFromJsonAsync<User>();
        fetchedUser.Should().NotBeNull();
        fetchedUser.CountryIsoCode.Should().Be("US");
        fetchedUser.Locale.Should().Be("ru-RU");
    }

    /// <summary>
    /// Проверяет обновление только локали при сохранении неизменным прежнего значения кода страны
    /// согласно п. 120 test-cases.md: Locale обновлена, CountryIsoCode сохранен, DateModified зафиксирован.
    /// </summary>
    [Fact]
    public async Task Update_OnlyLocale_MaintainsExistingCountryIsoCode()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        var updateDto = new UserDto(createdUser.CountryIsoCode, "en-US");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser.UserId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedUser = await response.Content.ReadFromJsonAsync<User>();
        updatedUser.Should().NotBeNull();
        updatedUser.UserId.Should().Be(createdUser.UserId);
        updatedUser.CountryIsoCode.Should().Be("RU");
        updatedUser.Locale.Should().Be("en-US");
        updatedUser.DateCreated.Should().Be(createdUser.DateCreated);
        updatedUser.DateModified.Should().NotBeNull();
        updatedUser.DateModified.Should().Be(createdUser.DateCreated.AddSeconds(5));
        updatedUser.DateModified!.Value.Kind.Should().Be(DateTimeKind.Utc);

        var getResponse = await _client.GetAsync($"/api/users/{createdUser.UserId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedUser = await getResponse.Content.ReadFromJsonAsync<User>();
        fetchedUser.Should().NotBeNull();
        fetchedUser.CountryIsoCode.Should().Be("RU");
        fetchedUser.Locale.Should().Be("en-US");
    }

    /// <summary>
    /// Проверяет, что передача невалидного Guid в маршруте PUT-запроса возвращает 400 Bad Request.
    /// </summary>
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")] // Guid.Empty
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("12345678-1234-1234-1234-12345678901z")]
    [InlineData("12345678-1234-1234-1234-12345678901!")]
    [InlineData("12345678-1234-1234-1234-1234567890123")]
    [InlineData("12345678-1234-1234-1234")]
    [InlineData(" ")]
    public async Task Update_InvalidGuid_Returns400BadRequest(string invalidId)
    {
        // Arrange
        var validDto = new UserDto("US", "en-US");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{Uri.EscapeDataString(invalidId)}", validDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Проверяет, что попытка обновления по валидному, но несуществующему Guid возвращает 404 Not Found.
    /// </summary>
    [Fact]
    public async Task Update_NonExistentGuid_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateDto = new UserDto("US", "en-US");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{nonExistentId}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Проверяет, что при передаче невалидного тела DTO в PUT-запросе возвращается 400 Bad Request.
    /// </summary>
    [Theory]
    // Невалидные CountryIsoCode:
    [InlineData("ru-RU", "11", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "ыы", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "rrr", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "r", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "RU\n", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", " ", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", " RU", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "RU ", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "R1", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", "1R", nameof(UserDto.CountryIsoCode))]
    [InlineData("ru-RU", null, nameof(UserDto.CountryIsoCode))]
    // Невалидная Locale:
    [InlineData("11-11", "RU", nameof(UserDto.Locale))]
    [InlineData("ru-ru", "RU", nameof(UserDto.Locale))]
    [InlineData("RU-RU", "RU", nameof(UserDto.Locale))]
    [InlineData("rU-rU", "RU", nameof(UserDto.Locale))]
    [InlineData("ыы-ЫЫ", "RU", nameof(UserDto.Locale))]
    [InlineData("ru-RURU", "RU", nameof(UserDto.Locale))]
    [InlineData("ru", "RU", nameof(UserDto.Locale))]
    [InlineData("ru_RU", "RU", nameof(UserDto.Locale))]
    [InlineData("ru|RU", "RU", nameof(UserDto.Locale))]
    [InlineData("ruRU", "RU", nameof(UserDto.Locale))]
    [InlineData("ru-RU\n", "RU", nameof(UserDto.Locale))]
    [InlineData("", "RU", nameof(UserDto.Locale))]
    [InlineData(" ", "RU", nameof(UserDto.Locale))]
    [InlineData("r-RU", "RU", nameof(UserDto.Locale))]
    [InlineData("rus-RU", "RU", nameof(UserDto.Locale))]
    [InlineData("ru-R", "RU", nameof(UserDto.Locale))]
    [InlineData("ru-RUS", "RU", nameof(UserDto.Locale))]
    [InlineData(null, "RU", nameof(UserDto.Locale))]
    // Оба поля невалидны:
    [InlineData("invalid", "1", "both")]
    [InlineData("", "", "both")]
    [InlineData(" ", " ", "both")]
    [InlineData("11-11", "11", "both")]
    [InlineData(null, null, "both")]
    public async Task Update_InvalidDto_Returns400BadRequest(string? locale, string? countryIsoCode, string expectedErrorProperty)
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        var invalidDto = new UserDto(countryIsoCode!, locale!);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser.UserId}", invalidDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);

        if (expectedErrorProperty == "both")
        {
            problemDetails.Errors.Keys.Should().Contain(k => string.Equals(k, nameof(UserDto.CountryIsoCode), StringComparison.OrdinalIgnoreCase));
            problemDetails.Errors.Keys.Should().Contain(k => string.Equals(k, nameof(UserDto.Locale), StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            problemDetails.Errors.Keys.Should().Contain(k => string.Equals(k, expectedErrorProperty, StringComparison.OrdinalIgnoreCase));
        }

        var checkResponse = await _client.GetAsync($"/api/users/{createdUser.UserId}");
        checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unchangedUser = await checkResponse.Content.ReadFromJsonAsync<User>();
        unchangedUser.Should().NotBeNull();
        unchangedUser.UserId.Should().Be(createdUser.UserId);
        unchangedUser.CountryIsoCode.Should().Be(createdUser.CountryIsoCode);
        unchangedUser.Locale.Should().Be(createdUser.Locale);
        unchangedUser.DateCreated.Should().Be(createdUser.DateCreated);
        unchangedUser.DateModified.Should().BeNull();
    }
}
