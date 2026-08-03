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
    public class EventsController(IEventService eventService) : ControllerBase
    {
        /// <summary>
        /// Возвращает список всех событий.
        /// </summary>
        /// <returns>HTTP-ответ со списком событий или описанием ошибки.</returns>
        [HttpGet]
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
        /// <returns>HTTP-ответ с событием или описанием ошибки.</returns>
        [HttpGet("{id}")]
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
        /// <returns>HTTP-ответ с созданным событием или описанием ошибки.</returns>
        [HttpPost]
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
        /// <returns>HTTP-ответ с обновлённым событием или описанием ошибки.</returns>
        [HttpPut("{id}")]
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
        /// <returns>HTTP-ответ без тела или описание ошибки.</returns>
        [HttpDelete("{id}")]
        public IActionResult DeleteEvent(Guid id)
        {
            ServiceResult<bool> result = eventService.DeleteEvent(id);

            if (!result.Success)
                return StatusCode(result.StatusCode, result.Error);

            return NoContent();
        }
    }
}
