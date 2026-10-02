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
        /// <param name="page">Номер возвращаемой страницы.</param>
        /// <param name="pageSize">Количество событий на странице.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Страница событий и сведения о пагинации.</returns>
        Task<PaginatedResult> GetEventsAsync(
            string? title = null,
            DateTime? from = null,
            DateTime? to = null,
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default);

        /// <summary>Получает событие по идентификатору.</summary>
        /// <param name="id">Идентификатор события.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат с DTO события или ошибкой, если событие не найдено.</returns>
        Task<ServiceResult<EventDto>> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Создаёт новое событие.</summary>
        /// <param name="event">DTO с данными создаваемого события.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат с DTO созданного события или ошибкой.</returns>
        Task<ServiceResult<EventDto>> CreateEventAsync(CreateEventDto @event, CancellationToken cancellationToken = default);

        /// <summary>Полностью обновляет существующее событие.</summary>
        /// <param name="id">Идентификатор обновляемого события.</param>
        /// <param name="dto">DTO с новыми данными события.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат с DTO обновлённого события или ошибкой.</returns>
        Task<ServiceResult<EventDto>> UpdateEventAsync(Guid id, UpdateEventDto dto, CancellationToken cancellationToken = default);

        /// <summary>Удаляет событие по идентификатору.</summary>
        /// <param name="id">Идентификатор удаляемого события.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат удаления или ошибкой, если событие не найдено.</returns>
        Task<ServiceResult> DeleteEventAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
