using EventManager.Api.Models;

namespace EventManager.Api.DataAccess.Repositories
{
    /// <summary>Предоставляет необходимые сценариям операции доступа к бронированиям без сохранения изменений.</summary>
    public interface IBookingRepository
    {
        /// <summary>Получает бронирование без отслеживания или null, если оно отсутствует.</summary>
        Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Получает отслеживаемое бронирование для изменения.</summary>
        Task<Booking?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>Получает материализованные идентификаторы ожидающих бронирований без отслеживания.</summary>
        Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken);

        /// <summary>Добавляет бронирование в общий контекст без сохранения.</summary>
        void Add(Booking booking);
    }
}
