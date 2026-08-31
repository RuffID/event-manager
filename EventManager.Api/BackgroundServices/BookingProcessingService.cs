using EventManager.Api.Models;
using EventManager.Api.Repositories;

namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Периодически обрабатывает созданные бронирования.
    /// </summary>
    /// <param name="bookingRepository">Хранилище бронирований в памяти.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingProcessingService(
        InMemoryBookingRepository bookingRepository,
        ILogger<BookingProcessingService> logger) : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(PollingInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    List<Booking> pendingBookings = bookingRepository.Bookings.Values
                        .Where(booking => booking.Status == BookingStatus.Pending)
                        .ToList();

                    foreach (Booking booking in pendingBookings)
                    {
                        await Task.Delay(ProcessingDelay, stoppingToken);

                        booking.Confirm();
                        bookingRepository.Bookings[booking.Id] = booking;

                        logger.LogInformation("Бронь {BookingId} подтверждена.", booking.Id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Фоновая обработка бронирований остановлена.");
            }
        }
    }
}
