namespace EventManager.Api.Models.Dtos
{
    /// <summary>
    /// Представляет данные брони, возвращаемые клиенту API.
    /// </summary>
    public class BookingInfo
    {
        /// <summary>Получает или задаёт идентификатор брони.</summary>
        public Guid Id { get; set; }

        /// <summary>Получает или задаёт идентификатор забронированного события.</summary>
        public Guid EventId { get; set; }

        /// <summary>Получает или задаёт статус брони.</summary>
        public BookingStatus Status { get; set; }

        /// <summary>Получает или задаёт дату и время создания брони.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Получает или задаёт дату и время обработки брони.</summary>
        public DateTime? ProcessedAt { get; set; }
    }
}
