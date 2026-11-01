using FluentValidation;
using Scalar.AspNetCore;
using UserManagement.API.DTOs;
using UserManagement.API.Interfaces;
using UserManagement.API.Middlewares;
using UserManagement.API.Repositories;
using UserManagement.API.Services;
using UserManagement.API.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IValidator<UserDto>, UserValidator>();
builder.Services.AddScoped<IValidator<string>, StringGuidValidator>();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IUserService, UserService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// для тестов
public partial class Program { }
