namespace EventManager.Api.Models
{
    /// <summary>
    /// Представляет событие, хранимое в приложении.
    /// </summary>
    public class Event
    {
        /// <summary>Получает или задаёт уникальный идентификатор события.</summary>
        public required Guid Id { get; set; }

        /// <summary>Получает или задаёт название события.</summary>
        public required string Title { get; set; }

        /// <summary>Получает или задаёт описание события.</summary>
        public string? Description { get; set; }

        /// <summary>Получает или задаёт дату и время начала события.</summary>
        public required DateTime StartAt { get; set; }

        /// <summary>Получает или задаёт дату и время окончания события.</summary>
        public required DateTime EndAt { get; set; }
    }
}
