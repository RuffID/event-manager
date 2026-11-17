using System.Collections.Concurrent;

namespace EventManager.Application.Services
{
    /// <summary>Синхронизирует резервирование и возврат мест между контекстами внутри процесса.</summary>
    internal static class BookingSynchronization
    {
        internal static readonly SemaphoreSlim SeatSemaphore = new(1, 1);
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> BookingSemaphores = new();

        /// <summary>Сериализует обработку и отмену одной брони, сохраняя параллельность разных броней.</summary>
        internal static SemaphoreSlim GetBookingSemaphore(Guid bookingId)
        {
            return BookingSemaphores.GetOrAdd(bookingId, _ => new SemaphoreSlim(1, 1));
        }
    }
}
