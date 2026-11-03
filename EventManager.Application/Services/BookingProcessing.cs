using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EventManager.Application.Services
{
    /// <summary>Выполняет обработку брони и компенсацию после ошибки в переданной единице работы.</summary>
    /// <param name="bookingRepository">Репозиторий бронирований текущего scope.</param>
    /// <param name="eventRepository">Репозиторий событий текущего scope.</param>
    /// <param name="unitOfWork">Общая граница сохранения изменений.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingProcessing(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        IUnitOfWork unitOfWork,
        ILogger<BookingProcessing> logger)
    {
        /// <summary>Повторно проверяет состояние брони, подтверждает или отклоняет её и сохраняет результат.</summary>
        /// <param name="bookingId">Идентификатор брони.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        public async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
        {
            Booking? booking = await bookingRepository.GetByIdForUpdateAsync(bookingId, cancellationToken);
            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                return;
            }

            bool eventExists = await eventRepository.ExistsAsync(booking.EventId, cancellationToken);
            if (!eventExists)
            {
                booking.Reject();
            }
            else
            {
                booking.Confirm();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Бронь {BookingId} обработана со статусом {Status}.", bookingId, booking.Status);
        }

        /// <summary>В отдельной единице работы отклоняет ожидающую бронь и возвращает место после ошибки.</summary>
        /// <param name="bookingId">Идентификатор брони.</param>
        public async Task RejectBookingAfterFailureAsync(Guid bookingId)
        {
            // Компенсация после ошибки должна завершиться даже при остановке приложения.
            // Использует тот же семафор, что и создание брони, до чтения и сохранения данных.
            await BookingSynchronization.SeatSemaphore.WaitAsync(CancellationToken.None);

            try
            {
                Booking? booking = await bookingRepository.GetByIdForUpdateAsync(bookingId, CancellationToken.None);
                if (booking is null || booking.Status != BookingStatus.Pending)
                {
                    return;
                }

                booking.Reject();
                Event? @event = await eventRepository.GetByIdForUpdateAsync(booking.EventId, CancellationToken.None);

                @event?.ReleaseSeats();

                await unitOfWork.SaveChangesAsync(CancellationToken.None);
                logger.LogWarning("Бронь {BookingId} отклонена после ошибки.", booking.Id);
            }
            finally
            {
                BookingSynchronization.SeatSemaphore.Release();
            }
        }
    }
}
