using EventManager.Api.Extensions;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Api.Controllers
{
    /// <summary>Обрабатывает регистрацию пользователей и вход по логину и паролю.</summary>
    /// <param name="userService">Сервис регистрации и входа.</param>
    [Route("[controller]")]
    [ApiController]
    [AllowAnonymous]
    [Produces("application/json")]
    public class AuthController(IUserService userService) : ControllerBase
    {
        /// <summary>Регистрирует пользователя с ролью User по умолчанию; учебное ТЗ также разрешает выбрать Admin.</summary>
        /// <param name="dto">Логин, пароль и необязательная роль.</param>
        /// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
        /// <response code="201">Пользователь зарегистрирован; пароль и его хеш в ответ не включаются.</response>
        /// <response code="400">Переданы невалидные данные.</response>
        /// <response code="409">Логин уже занят.</response>
        [HttpPost("register")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(UserInfo), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto, CancellationToken cancellationToken = default)
        {
            ServiceResult<UserInfo> result = await userService.RegisterAsync(dto, cancellationToken);
            return this.ToActionResult(result, user => StatusCode(StatusCodes.Status201Created, user));
        }

        /// <summary>Проверяет логин и пароль и возвращает JWT для доступа к защищённым операциям.</summary>
        /// <param name="dto">Учётные данные пользователя.</param>
        /// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
        /// <response code="200">Возвращает JWT в поле token.</response>
        /// <response code="400">Переданы невалидные данные.</response>
        /// <response code="401">Логин или пароль неверны.</response>
        [HttpPost("login")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken = default)
        {
            ServiceResult<TokenDto> result = await userService.LoginAsync(dto, cancellationToken);
            return this.ToActionResult(result, token => Ok(token));
        }
    }
}
