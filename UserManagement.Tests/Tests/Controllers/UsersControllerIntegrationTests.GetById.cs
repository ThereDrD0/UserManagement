using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserManagement.API.Models;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests.Controllers;

public sealed partial class UsersControllerIntegrationTests
{
    /// <summary>
    /// Проверяет успешное получение ранее созданного пользователя по его идентификатору.
    /// </summary>
    [Fact]
    public async Task GetById_ExistingUser_Returns200OkWithMatchingFields()
    {
        // Arrange
        var createDto = TestConstants.DefaultValidUserDto;
        var createResponse = await _client.PostAsJsonAsync("/api/users", createDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        // Act
        var response = await _client.GetAsync($"/api/users/{createdUser.UserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetchedUser = await response.Content.ReadFromJsonAsync<User>();
        fetchedUser.Should().NotBeNull();
        fetchedUser.UserId.Should().Be(createdUser.UserId);
        fetchedUser.CountryIsoCode.Should().Be(createdUser.CountryIsoCode);
        fetchedUser.Locale.Should().Be(createdUser.Locale);
        fetchedUser.DateCreated.Should().Be(createdUser.DateCreated);
        fetchedUser.DateModified.Should().BeNull();
    }

    /// <summary>
    /// Проверяет успешное получение пользователя при передаче GUID в верхнем регистре
    /// </summary>
    [Fact]
    public async Task GetById_GuidInUppercase_Returns200Ok()
    {
        // Arrange
        var createDto = TestConstants.DefaultValidUserDto;
        var createResponse = await _client.PostAsJsonAsync("/api/users", createDto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();

        // Act
        var response = await _client.GetAsync($"/api/users/{createdUser.UserId.ToString().ToUpperInvariant()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetchedUser = await response.Content.ReadFromJsonAsync<User>();
        fetchedUser.Should().NotBeNull();
        fetchedUser.UserId.Should().Be(createdUser.UserId);
    }

    /// <summary>
    /// Проверяет, что передача невалидного строкового представления Guid в маршруте
    /// приводит к ответу 400 Bad Request.
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
    public async Task GetById_InvalidGuid_Returns400BadRequest(string invalidId)
    {
        // Arrange
        var requestUri = $"/api/users/{Uri.EscapeDataString(invalidId)}";

        // Act
        var response = await _client.GetAsync(requestUri);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Проверяет, что при запросе несуществующего идентификатора возвращается 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetById_NonExistentGuid_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
