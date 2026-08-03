namespace EventManager.Api.Models
{
    /// <summary>
    /// Представляет событие, хранимое в приложении.
    /// </summary>
    public class Event
    {
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

        /// <summary>Создаёт событие в корректном состоянии.</summary>
        /// <param name="id">Уникальный идентификатор события.</param>
        /// <param name="title">Название события.</param>
        /// <param name="description">Описание события.</param>
        /// <param name="startAt">Дата и время начала события.</param>
        /// <param name="endAt">Дата и время окончания события.</param>
        public Event(Guid id, string title, string? description, DateTime startAt, DateTime endAt)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Event identifier must not be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Event title must not be empty.", nameof(title));

            if (endAt <= startAt)
                throw new ArgumentException("The end date must be later than the start date.", nameof(endAt));

            Id = id;
            Title = title;
            Description = description;
            StartAt = startAt;
            EndAt = endAt;
        }
    }
}
