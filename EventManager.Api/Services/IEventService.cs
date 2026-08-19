using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Определяет операции для работы с событиями.
    /// </summary>
    public interface IEventService
    {
        /// <summary>Получает список событий с учётом заданных фильтров.</summary>
        /// <param name="title">Часть названия события.</param>
        /// <param name="from">Минимальная дата и время начала события.</param>
        /// <param name="to">Максимальная дата и время окончания события.</param>
        /// <returns>Список DTO событий.</returns>
        List<EventDto> GetEvents(string? title = null, DateTime? from = null, DateTime? to = null);

        /// <summary>Получает событие по идентификатору.</summary>
        /// <param name="id">Идентификатор события.</param>
        /// <returns>Результат с DTO события или ошибкой, если событие не найдено.</returns>
        ServiceResult<EventDto> GetEventById(Guid id);

        /// <summary>Создаёт новое событие.</summary>
        /// <param name="event">DTO с данными создаваемого события.</param>
        /// <returns>Результат с DTO созданного события или ошибкой.</returns>
        ServiceResult<EventDto> CreateEvent(CreateEventDto @event);

        /// <summary>Полностью обновляет существующее событие.</summary>
        /// <param name="id">Идентификатор обновляемого события.</param>
        /// <param name="dto">DTO с новыми данными события.</param>
        /// <returns>Результат с DTO обновлённого события или ошибкой.</returns>
        ServiceResult<EventDto> UpdateEvent(Guid id, UpdateEventDto dto);

        /// <summary>Удаляет событие по идентификатору.</summary>
        /// <param name="id">Идентификатор удаляемого события.</param>
        /// <returns>Результат удаления или ошибкой, если событие не найдено.</returns>
        ServiceResult DeleteEvent(Guid id);
    }
}
