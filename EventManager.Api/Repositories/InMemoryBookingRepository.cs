using EventManager.Api.Models;
using System.Collections.Concurrent;

namespace EventManager.Api.Repositories
{
    /// <summary>
    /// Хранит бронирования в памяти приложения.
    /// </summary>
    public class InMemoryBookingRepository
    {
        /// <summary>
        /// Получает потокобезопасную коллекцию бронирований, где ключом является идентификатор брони.
        /// </summary>
        public ConcurrentDictionary<Guid, Booking> Bookings { get; } = new();
    }
}
