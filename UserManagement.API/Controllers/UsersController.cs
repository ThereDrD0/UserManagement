using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagement.API.DTOs;
using UserManagement.API.Interfaces;
using UserManagement.API.Models;

namespace UserManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsersController(
    IUserRepository userRepository, 
    IUserService userService,
    IValidator<UserDto> userValidator,
    IValidator<string> stringGuidValidator,
    ILogger<UsersController> logger) 
    : ControllerBase
{
    internal const int DefaultPage = 1;
    internal const int DefaultPageSize = 10;
    
    /// <summary>
    /// Создает нового пользователя с переданными данными в <see cref="UserDto"/>.
    /// </summary>
    /// <param name="userDto">Данные о пользователе, необходимые для его создания</param>
    /// <returns>Созданного пользователя</returns>
    [HttpPost]
    public ActionResult<User> Create(UserDto userDto)
    {
        logger.LogInformation("Запрос на создание нового пользователя: CountryIsoCode={CountryIsoCode}, Locale={Locale}", userDto.CountryIsoCode, userDto.Locale);

        var validResult = userValidator.Validate(userDto);
        if (!validResult.IsValid)
        {
            logger.LogWarning("Ошибка валидации при создании пользователя: {Errors}", validResult.ToDictionary());
            return ValidationProblem(new ValidationProblemDetails(validResult.ToDictionary()));
        }
        
        var user = userService.Create(userDto.CountryIsoCode, userDto.Locale);
        userRepository.Add(user);

        logger.LogInformation("Пользователь с Id: {UserId} успешно создан", user.UserId);

        return CreatedAtAction(nameof(GetById), new { id = user.UserId }, user);
    }

    /// <summary>
    /// Полностью обновляет текущего пользователя,
    /// заполняя его новыми данными <see cref="UserDto.CountryIsoCode"/> и <see cref="UserDto.Locale"/>
    /// </summary>
    /// <param name="id">Айди пользователя, который должен представлять валидный <see cref="Guid"/></param>
    /// <param name="userDto">Данные о пользователе, необходимые для его пересоздания</param>
    /// <returns>Обновленного пользователя</returns>
    [HttpPut("{id}")]
    public ActionResult<User> Update([FromRoute] string id, [FromBody] UserDto userDto)
    {
        logger.LogInformation("Запрос на обновление пользователя с Id: {UserId}", id);

        var guidValidResult = stringGuidValidator.Validate(id);
        if (!guidValidResult.IsValid)
        {
            logger.LogWarning("Ошибка валидации Id при обновлении пользователя: {Errors}", guidValidResult.ToDictionary());
            return ValidationProblem(new ValidationProblemDetails(guidValidResult.ToDictionary()));
        }

        var userValidResult = userValidator.Validate(userDto);
        if (!userValidResult.IsValid)
        {
            logger.LogWarning("Ошибка валидации данных при обновлении пользователя: {Errors}", userValidResult.ToDictionary());
            return ValidationProblem(new ValidationProblemDetails(userValidResult.ToDictionary()));
        }

        var guid = Guid.Parse(id);
        var user = userRepository.GetById(guid);

        if (user == null)
        {
            logger.LogWarning("Пользователь с Id: {UserId} не найден для обновления", id);
            return Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: "Не удалось найти пользователя с переданным id");
        }

        var updatedUser = userService.Update(user, userDto.CountryIsoCode, userDto.Locale);
        var updateSuccess = userRepository.Update(updatedUser);
        if (!updateSuccess)
        {
            logger.LogWarning("Не удалось обновить пользователя с Id: {UserId} в хранилище", id);
            return Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: "Не удалось обновить пользователя");
        }

        logger.LogInformation("Пользователь с Id: {UserId} успешно обновлен", updatedUser.UserId);
        return Ok(updatedUser);
    }

    /// <summary>
    /// Получает конкретного пользователя по его id
    /// </summary>
    /// <param name="id">Айди пользователя, который должен представлять валидный <see cref="Guid"/></param>
    /// <returns>Пользователя <see cref="User"/> имеющего заданный <see cref="id"/></returns>
    [HttpGet("{id}")]
    public ActionResult<User> GetById(string id)
    {
        logger.LogInformation("Запрос на получение пользователя с Id: {UserId}", id);

        var validResult = stringGuidValidator.Validate(id);
        if (!validResult.IsValid)
        {
            logger.LogWarning("Ошибка валидации Id при получении пользователя: {Errors}", validResult.ToDictionary());
            return ValidationProblem(new ValidationProblemDetails(validResult.ToDictionary()));
        }

        var guid = Guid.Parse(id);
        var user = userRepository.GetById(guid);
        
        if (user == null)
        {
            logger.LogWarning("Пользователь с Id: {UserId} не найден", id);
            return Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: "Не удалось найти пользователя с переданным id");
        }

        return Ok(user);
    }
    
    /// <summary>
    /// Получает пользователей с поддержкой пагинации или полный список при отсутствии параметров
    /// </summary>
    /// <param name="page">Номер страницы (начиная с 1)</param>
    /// <param name="pageSize">Размер страницы (начиная с 1)</param>
    /// <returns>Список пользователей</returns>
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetAll([FromQuery] int? page = null, [FromQuery] int? pageSize = null)
    {
        if (page == null && pageSize == null)
        {
            logger.LogInformation("Запрос на получение всех пользователей без пагинации");
            var users = userRepository.GetAll()
                .OrderBy(u => u.DateCreated)
                .ThenBy(u => u.UserId)
                .ToList();

            return Ok(users);
        }

        if (page < 1 || pageSize < 1)
        {
            logger.LogWarning("Ошибка валидации параметров пагинации: параметры page и pageSize должны быть больше или равны 1 (Page={Page}, PageSize={PageSize})", page, pageSize);
            ModelState.AddModelError("pagination", "Параметры page и pageSize должны быть больше или равны 1");

            return ValidationProblem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Параметры page и pageSize должны быть больше или равны 1",
                modelStateDictionary: ModelState);
        }

        var effectivePage = page ?? DefaultPage;
        var effectivePageSize = pageSize ?? DefaultPageSize;

        logger.LogInformation("Запрос пользователей с пагинацией: Page={Page}, PageSize={PageSize}", effectivePage, effectivePageSize);
        var skipCount = (long)(effectivePage - 1) * effectivePageSize;
        if (skipCount > int.MaxValue)
            return Ok(Enumerable.Empty<User>());

        var paginatedUsers = userRepository.GetAll()
            .OrderBy(u => u.DateCreated)
            .ThenBy(u => u.UserId)
            .Skip((int)skipCount)
            .Take(effectivePageSize)
            .ToList();

        return Ok(paginatedUsers);
    }
}
