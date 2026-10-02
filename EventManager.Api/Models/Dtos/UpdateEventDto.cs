using System.ComponentModel.DataAnnotations;

namespace EventManager.Api.Models.Dtos
{
    /// <summary>
    /// Представляет данные запроса на полное обновление события.
    /// </summary>
    public class UpdateEventDto
    {
        /// <summary>Получает или задаёт новое название события.</summary>
        [Required(ErrorMessage = "Specify the event title.")]
        [MaxLength(Event.MAX_TITLE_LENGTH, ErrorMessage = "Event title must not exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Получает или задаёт новое описание события.</summary>
        [MaxLength(Event.MAX_DESCRIPTION_LENGTH, ErrorMessage = "Event description must not exceed 2000 characters.")]
        public string? Description { get; set; }

        /// <summary>Получает или задаёт новые дату и время начала события.</summary>
        [Required(ErrorMessage = "Specify the start date.")]
        public DateTime? StartAt { get; set; }

        /// <summary>Получает или задаёт новые дату и время окончания события.</summary>
        [Required(ErrorMessage = "Specify the end date.")]
        public DateTime? EndAt { get; set; }
    }
}
