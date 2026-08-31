namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Периодически обрабатывает созданные бронирования.
    /// </summary>
    /// <param name="bookingProcessor">Обработчик ожидающих бронирований.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingProcessingService(
        BookingProcessor bookingProcessor,
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
