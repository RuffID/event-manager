using EventManager.Api.Extensions;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Api.Controllers
{
    /// <summary>
    /// Обрабатывает HTTP-запросы для работы с бронированиями.
    /// </summary>
    /// <param name="bookingService">Сервис для работы с бронированиями.</param>
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class BookingsController(IBookingService bookingService) : ControllerBase
    {
        /// <summary>
        /// Возвращает бронь владельцу или администратору по её идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор брони.</param>
        /// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
        /// <response code="200">Возвращает найденную бронь.</response>
        /// <response code="400">Передан невалидный идентификатор брони.</response>
        /// <response code="401">Текущий пользователь не определён.</response>
        /// <response code="403">Бронь принадлежит другому пользователю.</response>
        /// <response code="404">Бронь с указанным идентификатором не найдена.</response>
        /// <response code="500">Не удалось обработать запрос.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BookingInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBooking(Guid id, CancellationToken cancellationToken = default)
        {
            if (!User.TryGetUserId(out Guid userId))
            {
                return Unauthorized();
            }

            ServiceResult<BookingInfo> result = await bookingService.GetBookingByIdAsync(id, userId, User.IsInRole("Admin"), cancellationToken);
            return this.ToActionResult(result, booking => Ok(booking));
        }

        /// <summary>Отменяет свою бронь; администратор может отменить любую бронь.</summary>
        /// <param name="id">Идентификатор брони.</param>
        /// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
        /// <response code="204">Бронь отменена, место возвращено.</response>
        /// <response code="400">Передан невалидный идентификатор брони.</response>
        /// <response code="401">Текущий пользователь не определён.</response>
        /// <response code="403">Бронь принадлежит другому пользователю.</response>
        /// <response code="404">Бронь не найдена.</response>
        /// <response code="409">Бронь уже отменена или отклонена.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CancelBooking(Guid id, CancellationToken cancellationToken = default)
        {
            if (!User.TryGetUserId(out Guid userId))
            {
                return Unauthorized();
            }

            ServiceResult result = await bookingService.CancelBookingAsync(id, userId, User.IsInRole("Admin"), cancellationToken);
            return this.ToActionResult(result, NoContent);
        }
    }
}
