using EventManager.Api.Models;
using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Api.Services;

namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Обрабатывает ожидающие подтверждения бронирования.
    /// </summary>
    /// <param name="scopeFactory">Фабрика областей зависимостей для репозиториев и сохранения.</param>
    /// <param name="processingDelay">Искусственная задержка обработки.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingProcessor(
        IServiceScopeFactory scopeFactory,
        IBookingProcessingDelay processingDelay,
        ILogger<BookingProcessor> logger) : IDisposable
    {
        private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

        /// <summary>Обрабатывает текущий набор бронирований в статусе ожидания.</summary>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        public async Task ProcessPendingBookingsAsync(CancellationToken cancellationToken)
        {
            // Не допускает повторной обработки одного набора при пересечении запусков.
            await _processingSemaphore.WaitAsync(cancellationToken);

            try
            {
                IReadOnlyList<Guid> pendingBookingIds;
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    IBookingRepository bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                    pendingBookingIds = await bookingRepository.GetPendingIdsAsync(cancellationToken);
                }

                IEnumerable<Task> processingTasks = pendingBookingIds.Select(bookingId =>
                    ProcessBookingAsync(bookingId, cancellationToken));

                await Task.WhenAll(processingTasks);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        private async Task ProcessBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Начата обработка брони {BookingId}.", bookingId);

            try
            {
                await processingDelay.WaitAsync(cancellationToken);

                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IBookingRepository bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                IEventRepository eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
                IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Обработка брони {BookingId} отменена.", bookingId);
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "При обработке брони {BookingId} произошла ошибка.",
                    bookingId);

                try
                {
                    await RejectBookingAsync(bookingId);
                }
                catch (Exception rejectionException)
                {
                    logger.LogError(rejectionException,
                        "Не удалось отклонить бронь {BookingId} и вернуть место после ошибки.", bookingId);
                }
            }
        }

        private async Task RejectBookingAsync(Guid bookingId)
        {
            // Компенсация после ошибки должна завершиться даже при остановке приложения.
            await BookingSynchronization.SeatSemaphore.WaitAsync(CancellationToken.None);

            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IBookingRepository bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                IEventRepository eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
                IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

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

        /// <summary>Освобождает ресурсы синхронизации после завершения обработки.</summary>
        public void Dispose() => _processingSemaphore.Dispose();
    }
}
