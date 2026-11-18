namespace EventManager.Application.Services
{
    /// <summary>Синхронизирует резервирование и возврат мест между контекстами внутри процесса.</summary>
    internal static class BookingSynchronization
    {
        private const int BOOKING_SEMAPHORE_COUNT = 256;
        internal static readonly SemaphoreSlim SeatSemaphore = new(1, 1);
        private static readonly SemaphoreSlim[] BookingSemaphores = CreateBookingSemaphores();

        /// <summary>Выбирает постоянный семафор для брони; разные брони могут использовать один семафор.</summary>
        internal static SemaphoreSlim GetBookingSemaphore(Guid bookingId)
        {
            uint hash = unchecked((uint)bookingId.GetHashCode());

            return BookingSemaphores[hash % BOOKING_SEMAPHORE_COUNT];
        }

        /// <summary>Создаёт ограниченный набор семафоров на всё время работы процесса.</summary>
        private static SemaphoreSlim[] CreateBookingSemaphores()
        {
            SemaphoreSlim[] semaphores = new SemaphoreSlim[BOOKING_SEMAPHORE_COUNT];
            for (int index = 0; index < semaphores.Length; index++)
            {
                semaphores[index] = new SemaphoreSlim(1, 1);
            }

            return semaphores;
        }
    }
}
