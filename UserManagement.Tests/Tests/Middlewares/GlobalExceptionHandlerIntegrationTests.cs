using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using UserManagement.API.Interfaces;
using UserManagement.Tests.Infrastructure;

namespace UserManagement.Tests.Tests.Middlewares;

/// <summary>
/// Интеграционные тесты для глобального обработчика исключений <see cref="UserManagement.API.Middlewares.GlobalExceptionHandler"/>.
/// Проверяют перехват непредвиденных сбоев, возврат статуса 500 Internal Server Error
/// и соответствие контракту RFC ProblemDetails без утечки конфиденциальной отладочной информации.
/// </summary>
public sealed class GlobalExceptionHandlerIntegrationTests(CustomWebApplicationFactory factory) 
    : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>
    /// Проверяет, что при возникновении необработанного исключения в нижележащих сервисах
    /// глобальный middleware перехватывает сбой, устанавливает код 500 Internal Server Error,
    /// возвращает стандартизированный ProblemDetails и маскирует внутренние детали ошибки.
    /// </summary>
    [Fact]
    public async Task WhenRepositoryThrowsException_ServerReturns500InternalServerError()
    {
        // Arrange
        const string criticalErrorMessage = "Database critical disaster";

        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Мокаем репозиторий, чтобы сымитировать какую-то критическую ошибку и проверить работу обработчика.
                var mockRepo = new Mock<IUserRepository>();
                mockRepo.Setup(r => r.GetAll()).Throws(new Exception(criticalErrorMessage));
                
                services.RemoveAll<IUserRepository>();
                services.AddSingleton(mockRepo.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        // Проверяем, что вызов завершился нужной ошибкой
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Проверяем, что отданная пользователю ошибка содержит нужный статус код + имеет какое-то описание.
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Detail.Should().Be("Ошибка сервера, попробуйте позже!");

        // Проверяем, что вывод пользователю не содержит данные о внутренней инфраструктуре, т.к. это не безопасно
        var rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().NotContain(criticalErrorMessage);
        rawBody.Should().NotContain("StackTrace");
    }
}
