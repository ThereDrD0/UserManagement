using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UserManagement.API.Interfaces;
using UserManagement.API.Repositories;
using Xunit.Abstractions;

namespace UserManagement.Tests.Infrastructure;

/// <summary>
/// Кастомная фабрика веб-приложения для интеграционных тестов.
/// Управляет жизненным циклом тестового сервера и изолированных фабрик для тестов.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly ConcurrentBag<IDisposable> _disposables = [];

    /// <summary>
    /// Создает изолированный HTTP-клиент.
    /// Каждый клиент имеет свой <see cref="InMemoryUserRepository"/>, <see cref="ITestOutputHelper"/>
    /// и по желанию <see cref="TimeProvider"/> для текущего теста.
    /// Это гарантирует изоляцию данных и возможность параллельного выполнения тестов без взаимного влияния.
    /// </summary>
    public HttpClient CreateIsolatedClient(ITestOutputHelper output, TimeProvider? timeProvider = null)
    {
        var isolatedFactory = WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IUserRepository, InMemoryUserRepository>());

                if (timeProvider == null)
                    return;

                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            });

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddXUnit(output);
            });
        });

        _disposables.Add(isolatedFactory);

        return isolatedFactory.CreateClient();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            while (_disposables.TryTake(out var disposable))
            {
                disposable.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
