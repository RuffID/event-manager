namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Периодически обрабатывает созданные бронирования.
    /// </summary>
    /// <param name="scopeFactory">Фабрика областей зависимостей фоновой обработки.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingService> logger) : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                        BookingProcessor bookingProcessor = scope.ServiceProvider.GetRequiredService<BookingProcessor>();
                        await bookingProcessor.ProcessPendingBookingsAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Ошибка обработки ожидающих бронирований.");
                    }

                    await Task.Delay(PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Фоновая обработка бронирований остановлена.");
            }
        }
    }
}
