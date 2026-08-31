using EventManager.Api.Mappers;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Repositories;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Реализует операции создания и получения бронирований.
    /// </summary>
    /// <param name="eventRepository">Хранилище событий в памяти.</param>
    /// <param name="bookingRepository">Хранилище бронирований в памяти.</param>
    public class BookingService(
        InMemoryEventRepository eventRepository,
        InMemoryBookingRepository bookingRepository) : IBookingService
    {
        /// <inheritdoc />
        public Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId)
        {
            if (!eventRepository.Events.ContainsKey(eventId))
            {
                return Task.FromResult(
                    ServiceResult<BookingInfo>.Fail(
                        ServiceErrorType.NotFound,
                        "Event not found."));
            }

            Booking booking = new Booking(eventId);

            if (bookingRepository.Bookings.TryAdd(booking.Id, booking))
            {
                return Task.FromResult(
                    ServiceResult<BookingInfo>.Succeed(booking.ToInfo()));
            }

            return Task.FromResult(
                ServiceResult<BookingInfo>.Fail(
                    ServiceErrorType.Internal,
                    "Failed to create booking."));
        }

        /// <inheritdoc />
        public Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId)
        {
            if (bookingRepository.Bookings.TryGetValue(bookingId, out Booking? booking))
            {
                return Task.FromResult(
                    ServiceResult<BookingInfo>.Succeed(booking.ToInfo()));
            }

            return Task.FromResult(
                ServiceResult<BookingInfo>.Fail(
                    ServiceErrorType.NotFound,
                    "Booking not found."));
        }
    }
}
