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
        [MaxLength(Event.MAX_TITLE_LENGTH, ErrorMessage = "Event title must not exceed 200 characters.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Получает или задаёт описание создаваемого события.</summary>
        [MaxLength(Event.MAX_DESCRIPTION_LENGTH, ErrorMessage = "Event description must not exceed 2000 characters.")]
        public string? Description { get; set; }

        /// <summary>Получает или задаёт дату и время начала создаваемого события.</summary>
        [Required(ErrorMessage = "Specify the start date.")]
        public DateTime? StartAt { get; set; }

        /// <summary>Получает или задаёт дату и время окончания создаваемого события.</summary>
        [Required(ErrorMessage = "Specify the end date.")]
        public DateTime? EndAt { get; set; }

        /// <summary>Получает или задаёт общее количество мест на событии.</summary>
        [Required(ErrorMessage = "Specify the total number of seats.")]
        [Range(1, int.MaxValue, ErrorMessage = "The total number of seats must be greater than zero.")]
        public int? TotalSeats { get; set; }
    }
}
