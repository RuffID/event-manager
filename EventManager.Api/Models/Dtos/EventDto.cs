namespace EventManager.Api.Models.Dtos
{
    /// <summary>
    /// Представляет данные события, возвращаемые клиенту API.
    /// </summary>
    public class EventDto
    {
        /// <summary>Получает или задаёт идентификатор события.</summary>
        public Guid Id { get; set; }

        /// <summary>Получает или задаёт название события.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Получает или задаёт описание события.</summary>
        public string? Description { get; set; }

        /// <summary>Получает или задаёт дату и время начала события.</summary>
        public DateTime StartAt { get; set; }

        /// <summary>Получает или задаёт дату и время окончания события.</summary>
        public DateTime EndAt { get; set; }

        /// <summary>Получает или задаёт общее количество мест на событии.</summary>
        public int TotalSeats { get; set; }

        /// <summary>Получает или задаёт текущее количество свободных мест.</summary>
        public int AvailableSeats { get; set; }
    }
}
