using EventManager.Api.Extensions;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Api.Controllers
{
    /// <summary>
    /// Обрабатывает HTTP-запросы для работы с бронированиями.
    /// </summary>
    /// <param name="bookingService">Сервис для работы с бронированиями.</param>
    [Route("[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class BookingsController(IBookingService bookingService) : ControllerBase
    {
        /// <summary>
        /// Возвращает бронь по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор брони.</param>
        /// <response code="200">Возвращает найденную бронь.</response>
        /// <response code="404">Бронь с указанным идентификатором не найдена.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BookingInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBooking(Guid id)
        {
            ServiceResult<BookingInfo> result = await bookingService.GetBookingByIdAsync(id);
            return this.ToActionResult(result, booking => Ok(booking));
        }
    }
}
