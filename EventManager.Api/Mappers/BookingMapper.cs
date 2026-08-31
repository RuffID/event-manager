using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;

namespace EventManager.Api.Mappers
{
    /// <summary>
    /// Содержит методы преобразования сущностей бронирований и DTO.
    /// </summary>
    internal static class BookingMapper
    {
        /// <summary>Преобразует сущность брони в DTO для ответа API.</summary>
        /// <param name="booking">Сущность брони.</param>
        /// <returns>DTO с данными брони.</returns>
        public static BookingInfo ToInfo(this Booking booking)
        {
            return new BookingInfo
            {
                Id = booking.Id,
                EventId = booking.EventId,
                Status = booking.Status,
                CreatedAt = booking.CreatedAt,
                ProcessedAt = booking.ProcessedAt
            };
        }
    }
}
