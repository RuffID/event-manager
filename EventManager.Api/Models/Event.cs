using System.ComponentModel.DataAnnotations;

namespace EventManager.Api.Models
{
    /// <summary>
    /// Представляет событие, хранимое в приложении.
    /// </summary>
    public class Event
    {
        private readonly object _seatLock = new();
        private int _availableSeats;

        /// <summary>Получает уникальный идентификатор события.</summary>
        public Guid Id { get; }

        /// <summary>Получает название события.</summary>
        public string Title { get; }

        /// <summary>Получает описание события.</summary>
        public string? Description { get; }

        /// <summary>Получает дату и время начала события.</summary>
        public DateTime StartAt { get; }

        /// <summary>Получает дату и время окончания события.</summary>
        public DateTime EndAt { get; }

        /// <summary>Получает общее количество мест на событии.</summary>
        public int TotalSeats { get; }

        /// <summary>Получает текущее количество свободных мест.</summary>
        public int AvailableSeats
        {
            get
            {
                lock (_seatLock)
                {
                    return _availableSeats;
                }
            }
        }

        /// <summary>Создаёт событие в корректном состоянии.</summary>
        /// <param name="id">Уникальный идентификатор события.</param>
        /// <param name="title">Название события.</param>
        /// <param name="description">Описание события.</param>
        /// <param name="startAt">Дата и время начала события.</param>
        /// <param name="endAt">Дата и время окончания события.</param>
        /// <param name="totalSeats">Общее количество мест на событии.</param>
        public Event(
            Guid id,
            string title,
            string? description,
            DateTime startAt,
            DateTime endAt,
            int totalSeats)
            : this(id, title, description, startAt, endAt, totalSeats, totalSeats)
        {
        }

        private Event(
            Guid id,
            string title,
            string? description,
            DateTime startAt,
            DateTime endAt,
            int totalSeats,
            int availableSeats)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Event identifier must not be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Event title must not be empty.", nameof(title));

            if (endAt <= startAt)
                throw new ArgumentException("The end date must be later than the start date.", nameof(endAt));

            if (totalSeats <= 0)
                throw new ValidationException("The total number of seats must be greater than zero.");

            if (availableSeats < 0 || availableSeats > totalSeats)
                throw new ArgumentOutOfRangeException(
                    nameof(availableSeats),
                    "The number of available seats must be between zero and the total number of seats.");

            Id = id;
            Title = title.Trim();
            Description = description;
            StartAt = startAt;
            EndAt = endAt;
            TotalSeats = totalSeats;
            _availableSeats = availableSeats;
        }

        /// <summary>Создаёт новое событие с указанным количеством мест.</summary>
        /// <param name="title">Название события.</param>
        /// <param name="description">Описание события.</param>
        /// <param name="startAt">Дата и время начала события.</param>
        /// <param name="endAt">Дата и время окончания события.</param>
        /// <param name="totalSeats">Общее количество мест на событии.</param>
        /// <returns>Новое событие, у которого все места свободны.</returns>
        public static Event Create(
            string title,
            string? description,
            DateTime startAt,
            DateTime endAt,
            int totalSeats)
        {
            return new Event(
                Guid.NewGuid(),
                title,
                description,
                startAt,
                endAt,
                totalSeats);
        }

        /// <summary>Пытается зарезервировать указанное количество мест.</summary>
        /// <param name="count">Количество резервируемых мест.</param>
        /// <returns><see langword="true"/>, если места зарезервированы; иначе <see langword="false"/>.</returns>
        public bool TryReserveSeats(int count = 1)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

            lock (_seatLock)
            {
                if (_availableSeats < count)
                    return false;

                _availableSeats -= count;
                return true;
            }
        }

        /// <summary>Возвращает указанное количество мест в пул доступных.</summary>
        /// <param name="count">Количество освобождаемых мест.</param>
        public void ReleaseSeats(int count = 1)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

            lock (_seatLock)
            {
                if (count > TotalSeats - _availableSeats)
                    throw new InvalidOperationException("Cannot release more seats than have been reserved.");

                _availableSeats += count;
            }
        }

        /// <summary>Создаёт версию события с обновлёнными общими данными и прежним состоянием мест.</summary>
        /// <param name="title">Новое название события.</param>
        /// <param name="description">Новое описание события.</param>
        /// <param name="startAt">Новая дата и время начала события.</param>
        /// <param name="endAt">Новая дата и время окончания события.</param>
        /// <returns>Обновлённое событие.</returns>
        public Event UpdateDetails(
            string title,
            string? description,
            DateTime startAt,
            DateTime endAt)
        {
            lock (_seatLock)
            {
                return new Event(
                    Id,
                    title,
                    description,
                    startAt,
                    endAt,
                    TotalSeats,
                    _availableSeats);
            }
        }
    }
}
