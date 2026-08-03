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
        public string Title { get; set; } = string.Empty;

        /// <summary>Получает или задаёт новое описание события.</summary>
        public string? Description { get; set; }

        /// <summary>Получает или задаёт новые дату и время начала события.</summary>
        [Required(ErrorMessage = "Specify the start date.")]
        public DateTime? StartAt { get; set; }
        
        /// <summary>Получает или задаёт новые дату и время окончания события.</summary>
        [Required(ErrorMessage = "Specify the end date.")]
        public DateTime? EndAt { get; set; }
    }
}
