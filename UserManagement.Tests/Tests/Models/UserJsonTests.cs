using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using UserManagement.API.DTOs;
using UserManagement.Tests.Infrastructure;
using Xunit.Abstractions;

namespace UserManagement.Tests.Tests.Models;

public sealed class UserJsonTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new(TestConstants.DefaultTimeOffset);
    private readonly HttpClient _client;

    public UserJsonTests(CustomWebApplicationFactory factory, ITestOutputHelper outputHelper)
    {
        _client = factory.CreateIsolatedClient(outputHelper, _timeProvider);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
    
    /// <summary>
    /// Проверяет правильность и валидность JSON на всех 3 основных запросах - POST/GET/PUT.
    /// Последовательно проводит созданного пользователя через все запросы. <br/> <br/>
    /// Этапы: <br/>
    /// 1. Создание пользователя через POST /api/users. <br/>
    /// 2. Запрос созданного пользователя через GET /api/users/{id}. <br/>
    /// 3. Обновление пользователя через PUT /api/users/{id} новыми данными с выставлением DateModified.
    /// </summary>
    [Fact]
    public async Task PostGetPut_ValidRequest_ReturnsValidJsonWithAllProperties()
    {
        // 1. POST Arrange - тут нечего создавать
        // 1. POST Act
        var postResponse = await _client.PostAsJsonAsync("/api/users", TestConstants.DefaultValidUserDto);
        // 1. POST Assert
        var user = await postResponse.Check(false);
        var userGuid = user.UserId;


        // 2. GET Arrange - пропустили, потому что прошлый этап уже создал нужного пользователя.
        // 2. GET Act
        var getInitialResponse = await _client.GetAsync($"/api/users/{userGuid}");
        // 2. GET Assert
        await getInitialResponse.Check(false);

        // 3. PUT Arrange
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        var updateDto = new UserDto("us", "en-US");
        // 3. PUT Act
        var putResponse = await _client.PutAsJsonAsync($"/api/users/{userGuid}", updateDto);
        // 3. PUT Assert
        await putResponse.Check(true);
    }
}