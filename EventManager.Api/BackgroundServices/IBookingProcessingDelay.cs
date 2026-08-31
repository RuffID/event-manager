namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Определяет искусственное ожидание перед обработкой брони.
    /// </summary>
    public interface IBookingProcessingDelay
    {
        /// <summary>Ожидает завершения имитации внешнего вызова.</summary>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        Task WaitAsync(CancellationToken cancellationToken);
    }
}
