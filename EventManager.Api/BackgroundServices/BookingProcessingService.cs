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
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(PollingInterval);

            try
            {
                do
                {
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                    BookingProcessor bookingProcessor = scope.ServiceProvider.GetRequiredService<BookingProcessor>();
                    await bookingProcessor.ProcessPendingBookingsAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Фоновая обработка бронирований остановлена.");
            }
        }
    }
}
