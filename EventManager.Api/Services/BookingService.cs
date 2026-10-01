using EventManager.Api.Exceptions;
using EventManager.Api.Mappers;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Реализует операции создания и получения бронирований.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    public class BookingService(AppDbContext context) : IBookingService
    {
        private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

        /// <inheritdoc />
        public async Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId)
        {
            await BookingSemaphore.WaitAsync();

            try
            {
                Event? @event = await context.Events.SingleOrDefaultAsync(entity => entity.Id == eventId);

                if (@event is not null)
                {
                    // Обновляет состояние, если событие уже загружалось в текущем scope.
                    await context.Entry(@event).ReloadAsync();
                }

                if (@event is null || context.Entry(@event).State == EntityState.Detached)
                {
                    return ServiceResult<BookingInfo>.Fail(
                        ServiceErrorType.NotFound,
                        "Event not found.");
                }

                if (!@event.TryReserveSeats())
                    throw new NoAvailableSeatsException();

                Booking booking = new Booking(eventId);

                context.Bookings.Add(booking);
                await context.SaveChangesAsync();

                return ServiceResult<BookingInfo>.Succeed(booking.ToInfo());
            }
            finally
            {
                BookingSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId)
        {
            Booking? booking = await context.Bookings.AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == bookingId);

            if (booking is not null)
            {
                return ServiceResult<BookingInfo>.Succeed(booking.ToInfo());
            }

            return ServiceResult<BookingInfo>.Fail(
                ServiceErrorType.NotFound,
                "Booking not found.");
        }
    }
}
