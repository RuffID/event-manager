namespace EventManager.Api.Models
{
    /// <summary>
    /// Представляет бронь на событие.
    /// </summary>
    public class Booking
    {
        /// <summary>Получает уникальный идентификатор брони.</summary>
        public Guid Id { get; }

        /// <summary>Получает идентификатор забронированного события.</summary>
        public Guid EventId { get; }

        /// <summary>Получает текущий статус брони.</summary>
        public BookingStatus Status { get; private set; }

        /// <summary>Получает дату и время создания брони.</summary>
        public DateTime CreatedAt { get; }

        /// <summary>Получает дату и время обработки брони.</summary>
        public DateTime? ProcessedAt { get; private set; }

        /// <summary>
        /// Создаёт бронь в статусе ожидания обработки.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        public Booking(Guid eventId)
        {
            if (eventId == Guid.Empty)
                throw new ArgumentException("Event identifier must not be empty.", nameof(eventId));

            Id = Guid.NewGuid();
            EventId = eventId;
            Status = BookingStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
