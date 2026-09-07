namespace EventManager.Api.BackgroundServices
{
    /// <summary>
    /// Имитирует задержку обращения к внешней системе.
    /// </summary>
    public class BookingProcessingDelay : IBookingProcessingDelay
    {
        private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

        /// <inheritdoc />
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return Task.Delay(ProcessingDelay, cancellationToken);
        }
    }
}
