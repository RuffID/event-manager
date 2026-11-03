using EventManager.Application.Services;

namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Управляет scopes и параллельным запуском прикладной обработки ожидающих бронирований.
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
                    BookingProcessing bookingProcessing = scope.ServiceProvider.GetRequiredService<BookingProcessing>();
                    pendingBookingIds = await bookingProcessing.GetPendingBookingIdsAsync(cancellationToken);
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

                BookingProcessing bookingProcessing = scope.ServiceProvider.GetRequiredService<BookingProcessing>();
                await bookingProcessing.ProcessBookingAsync(bookingId, cancellationToken);
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
                    // После сбоя используется новый scope без несохранённых изменений обработки.
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

                    BookingProcessing bookingProcessing = scope.ServiceProvider.GetRequiredService<BookingProcessing>();
                    await bookingProcessing.RejectBookingAfterFailureAsync(bookingId);
                }
                catch (Exception rejectionException)
                {
                    logger.LogError(rejectionException,
                        "Не удалось отклонить бронь {BookingId} и вернуть место после ошибки.", bookingId);
                }
            }
        }

        /// <summary>Освобождает ресурсы синхронизации после завершения обработки.</summary>
        public void Dispose()
        {
            _processingSemaphore.Dispose();
        }
    }
}
