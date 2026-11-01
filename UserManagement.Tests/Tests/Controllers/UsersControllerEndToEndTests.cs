using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using UserManagement.API.DTOs;
using UserManagement.API.Models;
using UserManagement.Tests.Infrastructure;
using Xunit.Abstractions;

namespace UserManagement.Tests.Tests.Controllers;

/// <summary>
/// Сквозные End-to-End тесты полного жизненного цикла пользователя в API:
/// Создание (POST) -> Получение (GET) -> Обновление (PUT) -> Верификация (GET) -> Проверка в списке (GET All).
/// </summary>
public sealed class UsersControllerEndToEndTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new(TestConstants.DefaultTimeOffset);
    private readonly HttpClient _client;

    public UsersControllerEndToEndTests(CustomWebApplicationFactory factory, ITestOutputHelper outputHelper)
    {
        _client = factory.CreateIsolatedClient(outputHelper, _timeProvider);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    /// Проверяет полный сквозной сценарий жизненного цикла сущности пользователя:
    /// 1. Создание пользователя через POST /api/users (с нормализацией регистра кода страны).
    /// 2. Запрос созданного пользователя через GET /api/users/{id} и проверка соответствия полей.
    /// 3. Обновление пользователя через PUT /api/users/{id} новыми данными с выставлением DateModified.
    /// 4. Повторный запрос через GET /api/users/{id} для подтверждения персистентности изменений.
    /// 5. Запрос GET /api/users и проверка присутствия обновленного пользователя в общем списке.
    /// </summary>
    [Fact]
    public async Task EndToEnd_FullUserLifecycle_CreateGetUpdateVerifyList_ShouldSucceed()
    {
        // 1. POST Arrange
        var createDto = TestConstants.DefaultValidUserDto;

        // 1. POST Act
        var createResponse = await _client.PostAsJsonAsync("/api/users", createDto);

        // 1. POST Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>();
        createdUser.Should().NotBeNull();
        createdUser.UserId.Should().NotBeEmpty();
        createdUser.CountryIsoCode.Should().Be("RU");
        createdUser.Locale.Should().Be("ru-RU");
        createdUser.DateCreated.Kind.Should().Be(DateTimeKind.Utc);
        createdUser.DateCreated.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        createdUser.DateModified.Should().BeNull();


        // 2. GET Arrange - пропустили, потому что прошлый этап уже создал нужного пользователя.

        // 2. GET Act
        var getInitialResponse = await _client.GetAsync($"/api/users/{createdUser.UserId}");

        // 2. GET Assert
        getInitialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedInitialUser = await getInitialResponse.Content.ReadFromJsonAsync<User>();
        fetchedInitialUser.Should().NotBeNull();
        fetchedInitialUser.UserId.Should().Be(createdUser.UserId);
        fetchedInitialUser.CountryIsoCode.Should().Be("RU");
        fetchedInitialUser.Locale.Should().Be("ru-RU");
        fetchedInitialUser.DateCreated.Should().Be(createdUser.DateCreated);
        fetchedInitialUser.DateModified.Should().BeNull();


        // 3. PUT Arrange
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        var updateDto = new UserDto("us", "en-US");

        // 3. PUT Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/users/{createdUser.UserId}", updateDto);

        // 3. PUT Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedUser = await updateResponse.Content.ReadFromJsonAsync<User>();
        updatedUser.Should().NotBeNull();
        updatedUser.UserId.Should().Be(createdUser.UserId);
        updatedUser.CountryIsoCode.Should().Be("US");
        updatedUser.Locale.Should().Be("en-US");
        updatedUser.DateCreated.Should().Be(createdUser.DateCreated);
        updatedUser.DateModified.Should().NotBeNull();
        updatedUser.DateModified.Should().Be(createdUser.DateCreated.AddMinutes(5));
        updatedUser.DateModified!.Value.Kind.Should().Be(DateTimeKind.Utc);


        // 4. GET Arrange - пропустили,
        // потому что пользователь уже есть, мы смотрим что гет работает с уже измененным после PUT пользователем

        // 4. GET Act
        var getUpdatedResponse = await _client.GetAsync($"/api/users/{createdUser.UserId}");

        // 4. GET Assert
        getUpdatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedUpdatedUser = await getUpdatedResponse.Content.ReadFromJsonAsync<User>();
        fetchedUpdatedUser.Should().NotBeNull();
        fetchedUpdatedUser.UserId.Should().Be(createdUser.UserId);
        fetchedUpdatedUser.CountryIsoCode.Should().Be("US");
        fetchedUpdatedUser.Locale.Should().Be("en-US");
        fetchedUpdatedUser.DateCreated.Should().Be(createdUser.DateCreated);
        fetchedUpdatedUser.DateModified.Should().Be(updatedUser.DateModified);


        // 5. GET(All) Arrange - пропустили, потому что смотрим,
        // что выдача всех пользователей выдала единственного пользователя + он все тот же измененный пользователь

        // 5. GET(All) Act
        var getAllResponse = await _client.GetAsync("/api/users");

        // 5. GET(all) Assert
        getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allUsers = await getAllResponse.Content.ReadFromJsonAsync<List<User>>();
        allUsers.Should().NotBeNull();
        allUsers.Should().ContainSingle(u => u.UserId == createdUser.UserId);

        var userInList = allUsers.Single(u => u.UserId == createdUser.UserId);
        userInList.CountryIsoCode.Should().Be("US");
        userInList.Locale.Should().Be("en-US");
        userInList.DateCreated.Should().Be(createdUser.DateCreated);
        userInList.DateModified.Should().Be(updatedUser.DateModified);
    }
}
