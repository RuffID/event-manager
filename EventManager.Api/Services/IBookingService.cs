using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Определяет операции для работы с бронированиями.
    /// </summary>
    public interface IBookingService
    {
        /// <summary>Создаёт бронь для указанного события.</summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат с данными созданной брони или ошибкой.</returns>
        Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default);

        /// <summary>Получает бронь по идентификатору.</summary>
        /// <param name="bookingId">Идентификатор брони.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Результат с данными брони или ошибкой, если бронь не найдена.</returns>
        Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
    }
}
