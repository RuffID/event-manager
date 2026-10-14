using EventManager.Api.Exceptions;
using EventManager.Api.Mappers;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Реализует операции создания и получения бронирований.
    /// </summary>
    /// <param name="eventRepository">Репозиторий событий.</param>
    /// <param name="bookingRepository">Репозиторий бронирований.</param>
    /// <param name="unitOfWork">Общая граница сохранения изменений.</param>
    public class BookingService(
        IEventRepository eventRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork) : IBookingService
    {
        /// <inheritdoc />
        public async Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            await BookingSynchronization.SeatSemaphore.WaitAsync(cancellationToken);

            try
            {
                Event? @event = await eventRepository.GetCurrentByIdForUpdateAsync(eventId, cancellationToken);
                if (@event is null)
                {
                    return ServiceResult<BookingInfo>.Fail(
                        ServiceErrorType.NotFound,
                        "Event not found.");
                }

                if (!@event.TryReserveSeats())
                {
                    throw new NoAvailableSeatsException();
                }

                Booking booking = new Booking(eventId);

                bookingRepository.Add(booking);
                await unitOfWork.SaveChangesAsync(cancellationToken);

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
            Booking? booking = await bookingRepository.GetByIdAsync(bookingId, cancellationToken);
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
