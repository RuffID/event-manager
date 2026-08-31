using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using EventManager.Api.Extensions;
using System.ComponentModel.DataAnnotations;

namespace EventManager.Api.Controllers
{
    /// <summary>
    /// Обрабатывает HTTP-запросы для работы с событиями.
    /// </summary>
    /// <param name="eventService">Сервис для работы с событиями.</param>
    /// <param name="bookingService">Сервис для работы с бронированиями.</param>
    [Route("[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class EventsController(
        IEventService eventService,
        IBookingService bookingService) : ControllerBase
    {
        /// <summary>
        /// Возвращает список событий с учётом заданных фильтров.
        /// </summary>
        /// <param name="title">Часть названия события.</param>
        /// <param name="from">Минимальная дата и время начала события.</param>
        /// <param name="to">Максимальная дата и время окончания события.</param>
        /// <param name="page">Номер возвращаемой страницы.</param>
        /// <param name="pageSize">Количество событий на странице.</param>
        /// <response code="200">Возвращает список событий.</response>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult), StatusCodes.Status200OK)]
        public IActionResult GetEvents(
            [FromQuery] string? title = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery, Range(1, int.MaxValue)] int page = 1,
            [FromQuery, Range(1, int.MaxValue)] int pageSize = 10)
        {
            PaginatedResult result = eventService.GetEvents(title, from, to, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Возвращает событие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор события.</param>
        /// <response code="200">Возвращает найденное событие.</response>
        /// <response code="404">Событие с указанным идентификатором не найдено.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public IActionResult GetEvent(Guid id)
        {
            ServiceResult<EventDto> result = eventService.GetEventById(id);
            return this.ToActionResult(result, data => Ok(data));
        }

        /// <summary>
        /// Создаёт новое событие.
        /// </summary>
        /// <param name="dto">Данные создаваемого события.</param>
        /// <response code="201">Возвращает созданное событие.</response>
        /// <response code="400">Переданы невалидные данные события.</response>
        /// <response code="500">Не удалось сохранить событие.</response>
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public IActionResult CreateEvent([FromBody] CreateEventDto dto)
        {
            ServiceResult<EventDto> result = eventService.CreateEvent(dto);
            return this.ToActionResult(
                result,
                createdEvent => CreatedAtAction(
                    nameof(GetEvent),
                    new { id = createdEvent.Id },
                    createdEvent));
        }

        /// <summary>
        /// Создаёт бронь для указанного события.
        /// </summary>
        /// <param name="id">Идентификатор события.</param>
        /// <response code="202">Бронь создана и ожидает обработки.</response>
        /// <response code="404">Событие с указанным идентификатором не найдено.</response>
        /// <response code="500">Не удалось сохранить бронь.</response>
        [HttpPost("{id}/book")]
        [ProducesResponseType(typeof(BookingInfo), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateBooking(Guid id)
        {
            ServiceResult<BookingInfo> result = await bookingService.CreateBookingAsync(id);

            return this.ToActionResult(
                result,
                booking => AcceptedAtAction(
                    nameof(BookingsController.GetBooking),
                    "Bookings",
                    new { id = booking.Id },
                    booking));
        }

        /// <summary>
        /// Полностью обновляет событие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор обновляемого события.</param>
        /// <param name="dto">Новые данные события.</param>
        /// <response code="200">Возвращает обновлённое событие.</response>
        /// <response code="400">Переданы невалидные данные события.</response>
        /// <response code="404">Событие с указанным идентификатором не найдено.</response>
        [HttpPut("{id}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public IActionResult UpdateEvent(Guid id, [FromBody] UpdateEventDto dto)
        {
            ServiceResult<EventDto> result = eventService.UpdateEvent(id, dto);
            return this.ToActionResult(result, data => Ok(data));
        }

        /// <summary>
        /// Удаляет событие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор удаляемого события.</param>
        /// <response code="204">Событие успешно удалено. Тело ответа отсутствует.</response>
        /// <response code="404">Событие с указанным идентификатором не найдено.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public IActionResult DeleteEvent(Guid id)
        {
            ServiceResult result = eventService.DeleteEvent(id);
            return this.ToActionResult(result, NoContent);
        }
    }
}
