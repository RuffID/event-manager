using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;

namespace EventManager.Api.Mappers
{
    /// <summary>
    /// Содержит методы преобразования сущностей событий и DTO.
    /// </summary>
    internal static class EventMapper
    {
        /// <summary>Преобразует сущность события в DTO для ответа API.</summary>
        /// <param name="event">Сущность события.</param>
        /// <returns>DTO с данными события.</returns>
        public static EventDto ToDto(this Event @event)
        {
            return new EventDto
            {
                Id = @event.Id,
                Title = @event.Title,
                Description = @event.Description,
                StartAt = @event.StartAt,
                EndAt = @event.EndAt,
                TotalSeats = @event.TotalSeats,
                AvailableSeats = @event.AvailableSeats
            };
        }

        /// <summary>Создаёт сущность события из DTO запроса на создание.</summary>
        /// <param name="dto">DTO с данными создаваемого события.</param>
        /// <returns>Новая сущность события.</returns>
        public static Event ToEvent(this CreateEventDto dto)
        {
            return Event.Create(
                dto.Title,
                dto.Description,
                dto.StartAt!.Value,
                dto.EndAt!.Value,
                dto.TotalSeats!.Value);
        }

        /// <summary>Создаёт сущность события из DTO запроса на обновление.</summary>
        /// <param name="dto">DTO с новыми данными события.</param>
        /// <param name="event">Обновляемое событие.</param>
        /// <returns>Сущность события с обновлёнными данными.</returns>
        public static Event ToEvent(this UpdateEventDto dto, Event @event)
        {
            return @event.UpdateDetails(
                dto.Title,
                dto.Description,
                dto.StartAt!.Value,
                dto.EndAt!.Value);
        }
    }
}
