using System.ComponentModel.DataAnnotations;

namespace EventManager.Api.Models.Dtos
{
    /// <summary>
    /// Представляет данные запроса на создание события.
    /// </summary>
    public class CreateEventDto
    {
        /// <summary>Получает или задаёт название создаваемого события.</summary>
        [Required(ErrorMessage = "Specify the event title.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Получает или задаёт описание создаваемого события.</summary>
        public string? Description { get; set; }

        /// <summary>Получает или задаёт дату и время начала создаваемого события.</summary>
        [Required(ErrorMessage = "Specify the start date.")]
        public DateTime? StartAt { get; set; }
        
        /// <summary>Получает или задаёт дату и время окончания создаваемого события.</summary>
        [Required(ErrorMessage = "Specify the end date.")]
        public DateTime? EndAt { get; set; }
    }
}
