using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;

namespace EventManager.Application.Services
{
    /// <summary>
    /// Определяет операции для работы с бронированиями.
    /// </summary>
    public interface IBookingService
    {
        /// <summary>Создаёт бронь для указанного события.</summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="userId">Идентификатор пользователя из доверенного контекста.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат с данными созданной брони или ошибкой.</returns>
        Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Получает бронь по идентификатору.</summary>
        /// <param name="bookingId">Идентификатор брони.</param>
        /// <param name="userId">Идентификатор текущего пользователя.</param>
        /// <param name="isAdmin">Признак администратора из доверенного контекста.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат с данными брони или ошибкой, если бронь не найдена.</returns>
        Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);

        /// <summary>Отменяет свою бронь или любую бронь для администратора и возвращает место.</summary>
        /// <param name="bookingId">Идентификатор брони.</param>
        /// <param name="userId">Идентификатор текущего пользователя.</param>
        /// <param name="isAdmin">Признак администратора из доверенного контекста.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат отмены или ожидаемая ошибка.</returns>
        Task<ServiceResult> CancelBookingAsync(Guid bookingId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    }
}
