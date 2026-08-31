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
        /// <returns>Результат с данными созданной брони или ошибкой.</returns>
        Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId);

        /// <summary>Получает бронь по идентификатору.</summary>
        /// <param name="bookingId">Идентификатор брони.</param>
        /// <returns>Результат с данными брони или ошибкой, если бронь не найдена.</returns>
        Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId);
    }
}
