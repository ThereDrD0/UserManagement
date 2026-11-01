using Microsoft.Extensions.Time.Testing;
using UserManagement.Tests.Infrastructure;
using Xunit.Abstractions;

namespace UserManagement.Tests.Tests.Controllers;

/// <summary>
/// Интеграционные тесты для API контроллера <see cref="UserManagement.API.Controllers.UsersController"/>.
/// Проверяют полный цикл обработки HTTP-запросов, заголовки, валидацию и контракты ответов.
/// </summary>
public sealed partial class UsersControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new(TestConstants.DefaultTimeOffset);
    private readonly HttpClient _client;

    public UsersControllerIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper outputHelper)
    {
        _client = factory.CreateIsolatedClient(outputHelper, _timeProvider);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
