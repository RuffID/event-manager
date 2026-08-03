using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Api.Controllers
{
    /// <summary>
    /// Обрабатывает HTTP-запросы для работы с событиями.
    /// </summary>
    /// <param name="eventService">Сервис для работы с событиями.</param>
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class EventsController(IEventService eventService) : ControllerBase
    {
        /// <summary>
        /// Возвращает список всех событий.
        /// </summary>
        /// <response code="200">Возвращает список событий.</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
        public IActionResult GetEvents()
        {
            ServiceResult<List<EventDto>> result = eventService.GetEvents();
            
            if (!result.Success)
                return StatusCode(result.StatusCode, result.Error);

            return StatusCode(result.StatusCode, result.Data);
        }

        /// <summary>
        /// Возвращает событие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор события.</param>
        /// <response code="200">Возвращает найденное событие.</response>
        /// <response code="404">Событие с указанным идентификатором не найдено.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceError), StatusCodes.Status404NotFound)]
        public IActionResult GetEvent(Guid id)
        {
            ServiceResult<EventDto> result = eventService.GetEventById(id);

            if (!result.Success)
                return StatusCode(result.StatusCode, result.Error);

            return StatusCode(result.StatusCode, result.Data);
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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ServiceError), StatusCodes.Status500InternalServerError)]
        public IActionResult CreateEvent([FromBody] CreateEventDto dto)
        {
            ServiceResult<EventDto> result = eventService.CreateEvent(dto);

            if (!result.Success)
                return StatusCode(result.StatusCode, result.Error);

            return StatusCode(result.StatusCode, result.Data);
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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ServiceError), StatusCodes.Status404NotFound)]
        public IActionResult UpdateEvent(Guid id, [FromBody] UpdateEventDto dto)
        {
            ServiceResult<EventDto> result = eventService.UpdateEvent(id, dto);

            if (!result.Success)
                return StatusCode(result.StatusCode, result.Error);

            return StatusCode(result.StatusCode, result.Data);
        }

        /// <summary>
        /// Удаляет событие по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор удаляемого события.</param>
        /// <response code="204">Событие успешно удалено. Тело ответа отсутствует.</response>
        /// <response code="404">Событие с указанным идентификатором не найдено.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ServiceError), StatusCodes.Status404NotFound)]
        public IActionResult DeleteEvent(Guid id)
        {
            ServiceResult<bool> result = eventService.DeleteEvent(id);

            if (!result.Success)
                return StatusCode(result.StatusCode, result.Error);

            return NoContent();
        }
    }
}
