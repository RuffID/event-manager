namespace EventManager.Api.Services
{
    /// <summary>Синхронизирует резервирование и возврат мест между контекстами внутри процесса.</summary>
    internal static class BookingSynchronization
    {
        internal static readonly SemaphoreSlim SeatSemaphore = new(1, 1);
    }
}
