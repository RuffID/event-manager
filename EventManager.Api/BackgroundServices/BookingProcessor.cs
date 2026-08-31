using EventManager.Api.Models;
using EventManager.Api.Repositories;

namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Обрабатывает ожидающие подтверждения бронирования.
    /// </summary>
    /// <param name="bookingRepository">Хранилище бронирований в памяти.</param>
    /// <param name="processingDelay">Искусственная задержка обработки.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingProcessor(
        InMemoryBookingRepository bookingRepository,
        IBookingProcessingDelay processingDelay,
        ILogger<BookingProcessor> logger)
    {
        /// <summary>Обрабатывает текущий набор бронирований в статусе ожидания.</summary>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        public async Task ProcessPendingBookingsAsync(CancellationToken cancellationToken)
        {
            List<Booking> pendingBookings = bookingRepository.Bookings.Values
                .Where(booking => booking.Status == BookingStatus.Pending)
                .ToList();

            foreach (Booking booking in pendingBookings)
            {
                await processingDelay.WaitAsync(cancellationToken);

                try
                {
                    booking.Confirm();
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogWarning(
                        exception,
                        "Бронь {BookingId} уже была обработана и пропущена.",
                        booking.Id);
                    continue;
                }

                logger.LogInformation("Бронь {BookingId} подтверждена.", booking.Id);
            }
        }
    }
}
