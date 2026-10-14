using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;

namespace EventManager.Api.DataAccess.Repositories
{
    /// <summary>Предоставляет необходимые сценариям операции доступа к событиям без сохранения изменений.</summary>
    public interface IEventRepository
    {
        /// <summary>Получает событие без отслеживания или null, если оно отсутствует.</summary>
        Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Получает отслеживаемое событие для изменения или удаления.</summary>
        Task<Event?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Перезагружает отслеживаемое событие перед бронированием; возвращает null при его удалении.</summary>
        Task<Event?> GetCurrentByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Получает страницу без отслеживания с фильтрами и порядком StartAt, затем Id.</summary>
        Task<PaginatedResult> GetPageAsync(
            string? title,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken cancellationToken);

        /// <summary>Проверяет существование события.</summary>
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Добавляет событие в общий контекст без сохранения.</summary>
        void Add(Event @event);

        /// <summary>Помечает событие к удалению без сохранения.</summary>
        void Remove(Event @event);
    }
}
