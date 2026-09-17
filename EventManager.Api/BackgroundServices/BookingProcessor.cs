using EventManager.Api.Models;
using EventManager.Api.Repositories;

namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Обрабатывает ожидающие подтверждения бронирования.
    /// </summary>
    /// <param name="bookingRepository">Хранилище бронирований в памяти.</param>
    /// <param name="eventRepository">Хранилище событий в памяти.</param>
    /// <param name="processingDelay">Искусственная задержка обработки.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingProcessor(
        InMemoryBookingRepository bookingRepository,
        InMemoryEventRepository eventRepository,
        IBookingProcessingDelay processingDelay,
        ILogger<BookingProcessor> logger)
    {
        private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

        /// <summary>Обрабатывает текущий набор бронирований в статусе ожидания.</summary>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        public async Task ProcessPendingBookingsAsync(CancellationToken cancellationToken)
        {
            List<Booking> pendingBookings = bookingRepository.Bookings.Values
                .Where(booking => booking.Status == BookingStatus.Pending)
                .ToList();

            IEnumerable<Task> processingTasks = pendingBookings.Select(booking =>
                ProcessBookingAsync(booking, cancellationToken));

            await Task.WhenAll(processingTasks);
        }

        private async Task ProcessBookingAsync(
            Booking booking,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Начата обработка брони {BookingId}.", booking.Id);

            try
            {
                await processingDelay.WaitAsync(cancellationToken);
                await _processingSemaphore.WaitAsync(cancellationToken);

                try
                {
                    if (!eventRepository.Events.ContainsKey(booking.EventId))
                    {
                        booking.Reject();
                        bookingRepository.Bookings[booking.Id] = booking;

                        logger.LogWarning(
                            "Бронь {BookingId} отклонена: событие {EventId} не найдено.",
                            booking.Id,
                            booking.EventId);
                        return;
                    }

                    booking.Confirm();
                    bookingRepository.Bookings[booking.Id] = booking;
                }
                finally
                {
                    _processingSemaphore.Release();
                }

                logger.LogInformation("Бронь {BookingId} подтверждена.", booking.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Обработка брони {BookingId} отменена.", booking.Id);
                throw;
            }
            catch (InvalidOperationException exception)
                when (booking.Status != BookingStatus.Pending)
            {
                logger.LogWarning(
                    exception,
                    "Бронь {BookingId} уже была обработана и пропущена.",
                    booking.Id);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "При обработке брони {BookingId} произошла ошибка.",
                    booking.Id);

                await RejectBookingAsync(booking);
            }
        }

        private async Task RejectBookingAsync(Booking booking)
        {
            // Компенсация после ошибки должна завершиться даже при остановке приложения.
            await _processingSemaphore.WaitAsync(CancellationToken.None);

            try
            {
                if (booking.Status != BookingStatus.Pending)
                    return;

                booking.Reject();
                bookingRepository.Bookings[booking.Id] = booking;

                if (eventRepository.Events.TryGetValue(booking.EventId, out Event? @event))
                {
                    @event.ReleaseSeats();
                    eventRepository.Events[@event.Id] = @event;
                }

                logger.LogWarning("Бронь {BookingId} отклонена после ошибки.", booking.Id);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
    }
}
