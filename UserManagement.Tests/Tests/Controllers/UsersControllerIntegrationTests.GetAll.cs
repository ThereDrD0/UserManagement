using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UserManagement.API.Controllers;
using UserManagement.API.Models;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests.Controllers;

public sealed partial class UsersControllerIntegrationTests
{
    /// <summary>
    /// Проверяет, что при пустом репозитории запрос GET /api/users возвращает 200 OK и пустой массив [].
    /// </summary>
    [Fact]
    public async Task GetAll_EmptyRepository_Returns200OkWithEmptyList()
    {
        // Arrange: репозиторий изначально пуст для текущего изолированного теста

        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        users.Should().NotBeNull();
        users.Should().BeEmpty();
    }

    /// <summary>
    /// Проверяет, что при наличии нескольких пользователей GET /api/users возвращает их всех
    /// с детерминированным порядком сортировки по DateCreated, UserId.
    /// </summary>
    [Fact]
    public async Task GetAll_MultipleUsers_Returns200OkWithDeterministicallyOrderedUsers()
    {
        // Arrange
        var createdUsers = new List<User>();
        for (var i = 0; i < 3; i++)
        {
            var createResponse = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var user = await createResponse.Content.ReadFromJsonAsync<User>();
            user.Should().NotBeNull();
            createdUsers.Add(user);
            _timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        }

        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        users.Should().NotBeNull();
        users.Should().HaveCount(3);

        var expectedOrder = createdUsers
            .OrderBy(u => u.DateCreated)
            .ThenBy(u => u.UserId)
            .Select(u => u.UserId)
            .ToList();

        users.Select(u => u.UserId).Should().ContainInOrder(expectedOrder);
    }

    [Theory]
    // Проверка штатной ситуации
    [InlineData(1, 2, 5, 2)] // Страница 1 из 5 (размер 2) -> ровно первые 2 записи
    [InlineData(2, 2, 5, 2)] // Страница 2 из 5 (размер 2) -> ровно 3-я и 4-я записи
    [InlineData(3, 2, 5, 1)] // Страница 3 из 5 (размер 2) -> ровно 1 запись (остаток)
    [InlineData(4, 2, 5, 0)] // Страница 4 из 5 (размер 2) -> пустой список [] (выход за пределы)
    // Проверка дефолтов
    [InlineData(null, 2, 5, 2)] // pageSize = 2 (без page) -> первые 2 записи (дефолтный page = 1)
    [InlineData(null, null, 12, 12)]
    [InlineData(1, null, 12, UsersController.DefaultPageSize)]
    [InlineData(null, 3, 5, 3)] // количество пользователей в ответе > размера страницы
    [InlineData(null, 5, 3, 3)] // количество пользователей в ответе < размера страницы
    public async Task GetAll_WithValidPagination_ReturnsCorrectUserList(
        int? page, 
        int? pageSize, 
        int userCount, 
        int exceptedUsersOnPage)
    {
        // Arrange
        for (var i = 0; i < userCount; i++)
        {
            await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
        }

        var queryString = string.Empty;
        if (page != null && pageSize != null)
            queryString = $"?page={page}&pageSize={pageSize}";
        else if (page != null)
            queryString = $"?page={page}";
        else if (pageSize != null)
            queryString = $"?pageSize={pageSize}";

        // Act
        var response = await _client.GetAsync("/api/users" + queryString);
        var users = await response.Content.ReadFromJsonAsync<List<User>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        users.Should().NotBeNull();
        users.Should().HaveCount(exceptedUsersOnPage);
        
    }

    /// <summary>
    /// Проверяет, что при передаче невалидных параметров пагинации сервер возвращает 400 Bad Request.
    /// </summary>
    [Theory]
    [InlineData("?page=0&pageSize=10")]
    [InlineData("?page=-1&pageSize=10")]
    [InlineData("?page=abc&pageSize=10")]
    [InlineData("?page=1&pageSize=xyz")]
    [InlineData("?page=0&pageSize=2")]
    [InlineData("?page=1&pageSize=0")]
    [InlineData("?page=-1&pageSize=5")]
    [InlineData("?page=1&pageSize=-5")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=-5")]
    [InlineData("?page=abc&pageSize=2")]
    [InlineData("?page=1&pageSize=abc")]
    public async Task GetAllPagination_WithInvalidQuery_Returns400BadRequest(string queryString)
    {
        // Arrange
        var requestUri = $"/api/users{queryString}";

        // Act
        var response = await _client.GetAsync(requestUri);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
    }
}
