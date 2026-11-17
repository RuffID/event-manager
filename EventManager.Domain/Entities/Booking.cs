using EventManager.Domain.Enums;

namespace EventManager.Domain.Entities
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

        /// <summary>Получает идентификатор пользователя, которому принадлежит бронь.</summary>
        public Guid UserId { get; private set; }

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
        /// <remarks>Существующие сценарии используют этот конструктор до подключения пользователя в сервисах и API.</remarks>
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

        /// <summary>Создаёт бронь указанного пользователя в статусе ожидания обработки.</summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="userId">Идентификатор пользователя.</param>
        public Booking(Guid eventId, Guid userId)
            : this(eventId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User identifier must not be empty.", nameof(userId));
            }

            UserId = userId;
        }

        /// <summary>Отменяет бронь, сохраняя время её предыдущей обработки.</summary>
        public void Cancel()
        {
            lock (_stateLock)
            {
                if (_status == BookingStatus.Cancelled)
                {
                    throw new InvalidOperationException("The booking has already been cancelled.");
                }

                _status = BookingStatus.Cancelled;
            }
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

        /// <summary>Получает согласованный снимок статуса и времени обработки под одной блокировкой.</summary>
        /// <returns>Копия состояния обработки, изменение которой не влияет на бронь.</returns>
        public (BookingStatus Status, DateTime? ProcessedAt) GetProcessingState()
        {
            lock (_stateLock)
            {
                return (_status, _processedAt);
            }
        }
    }
}
