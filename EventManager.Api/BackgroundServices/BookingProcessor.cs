using EventManager.Api.Models;
using EventManager.Api.DataAccess;
using EventManager.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Обрабатывает ожидающие подтверждения бронирования.
    /// </summary>
    /// <param name="scopeFactory">Фабрика областей зависимостей для контекстов базы данных.</param>
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
                List<Guid> pendingBookingIds;
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    pendingBookingIds = await context.Bookings.AsNoTracking()
                        .Where(booking => booking.Status == BookingStatus.Pending)
                        .Select(booking => booking.Id)
                        .ToListAsync(cancellationToken);
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

                await RejectBookingAsync(bookingId);
                return;
            }

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Booking? booking = await context.Bookings
                .SingleOrDefaultAsync(entity => entity.Id == bookingId, cancellationToken);

            if (booking is null || booking.Status != BookingStatus.Pending)
                return;

            bool eventExists = await context.Events.AnyAsync(entity => entity.Id == booking.EventId, cancellationToken);

            if (!eventExists)
                booking.Reject();
            else
                booking.Confirm();

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Бронь {BookingId} обработана со статусом {Status}.", bookingId, booking.Status);
        }

        private async Task RejectBookingAsync(Guid bookingId)
        {
            // Компенсация после ошибки должна завершиться даже при остановке приложения.
            await BookingSynchronization.SeatSemaphore.WaitAsync(CancellationToken.None);

            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Booking? booking = await context.Bookings
                    .SingleOrDefaultAsync(entity => entity.Id == bookingId, CancellationToken.None);

                if (booking is null || booking.Status != BookingStatus.Pending)
                    return;

                booking.Reject();
                Event? @event = await context.Events
                    .SingleOrDefaultAsync(entity => entity.Id == booking.EventId, CancellationToken.None);

                @event?.ReleaseSeats();

                await context.SaveChangesAsync(CancellationToken.None);
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
