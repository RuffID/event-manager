namespace EventManager.Api.Models
{
    /// <summary>
    /// Представляет бронь на событие.
    /// </summary>
    public class Booking
    {
        private readonly Lock _stateLock = new();
        private BookingStatus _status;
        private DateTime? _processedAt;

        /// <summary>Получает уникальный идентификатор брони.</summary>
        public Guid Id { get; private set; }

        /// <summary>Получает идентификатор забронированного события.</summary>
        public Guid EventId { get; private set; }

        /// <summary>Получает событие бронирования после загрузки навигационного свойства.</summary>
        public Event Event { get; private set; } = null!;

        /// <summary>Получает текущий статус брони.</summary>
        public BookingStatus Status
        {
            get
            {
                lock (_stateLock)
                {
                    return _status;
                }
            }
        }

        /// <summary>Получает дату и время создания брони.</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>Получает дату и время обработки брони.</summary>
        public DateTime? ProcessedAt
        {
            get
            {
                lock (_stateLock)
                {
                    return _processedAt;
                }
            }
        }

        /// <summary>Создаёт экземпляр для материализации EF Core.</summary>
        private Booking()
        {
        }

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
            _status = BookingStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>Подтверждает бронь и фиксирует время обработки.</summary>
        public void Confirm()
        {
            Complete(BookingStatus.Confirmed);
        }

        /// <summary>Отклоняет бронь и фиксирует время обработки.</summary>
        public void Reject()
        {
            Complete(BookingStatus.Rejected);
        }

        private void Complete(BookingStatus status)
        {
            lock (_stateLock)
            {
                if (_status != BookingStatus.Pending)
                    throw new InvalidOperationException("Only a pending booking can be processed.");

                _processedAt = DateTime.UtcNow;
                _status = status;
            }
        }

        internal (BookingStatus Status, DateTime? ProcessedAt) GetProcessingState()
        {
            lock (_stateLock)
            {
                return (_status, _processedAt);
            }
        }
    }
}
