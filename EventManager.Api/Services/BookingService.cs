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
        /// <inheritdoc />
        public async Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            await BookingSynchronization.SeatSemaphore.WaitAsync(cancellationToken);

            try
            {
                Event? @event = await context.Events.SingleOrDefaultAsync(entity => entity.Id == eventId, cancellationToken);

                if (@event is not null)
                {
                    // Обновляет состояние, если событие уже загружалось в текущем scope.
                    await context.Entry(@event).ReloadAsync(cancellationToken);
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
                await context.SaveChangesAsync(cancellationToken);

                return ServiceResult<BookingInfo>.Succeed(booking.ToInfo());
            }
            finally
            {
                BookingSynchronization.SeatSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Booking? booking = await context.Bookings.AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == bookingId, cancellationToken);

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
