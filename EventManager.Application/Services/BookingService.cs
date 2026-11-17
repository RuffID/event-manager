using EventManager.Domain.Exceptions;
using EventManager.Application.Mappers;
using EventManager.Domain.Entities;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EventManager.Application.Services
{
    /// <summary>
    /// Реализует создание, получение и отмену бронирований с проверкой владельца и лимита.
    /// </summary>
    /// <param name="eventRepository">Репозиторий событий.</param>
    /// <param name="bookingRepository">Репозиторий бронирований.</param>
    /// <param name="unitOfWork">Общая граница сохранения изменений.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class BookingService(
        IEventRepository eventRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        ILogger<BookingService> logger) : IBookingService
    {
        /// <summary>Максимальное количество ожидающих и подтверждённых броней пользователя.</summary>
        public const int MAX_ACTIVE_BOOKINGS = 10;

        /// <inheritdoc />
        public async Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return ServiceResult<BookingInfo>.Fail(ServiceErrorType.Unauthorized, "User identifier is required.");
            }

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

                int activeCount = await bookingRepository.CountActiveByUserAsync(userId, cancellationToken);
                if (activeCount >= MAX_ACTIVE_BOOKINGS)
                {
                    logger.LogWarning("Booking limit reached for user {UserId}: {Limit}.", userId, MAX_ACTIVE_BOOKINGS);
                    throw new ActiveBookingLimitExceededException(MAX_ACTIVE_BOOKINGS);
                }

                @event.ReserveSeat(DateTime.UtcNow);
                Booking booking = new Booking(eventId, userId);

                bookingRepository.Add(booking);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Booking {BookingId} created for event {EventId} by user {UserId}.", booking.Id, eventId, userId);

                return ServiceResult<BookingInfo>.Succeed(booking.ToInfo());
            }
            finally
            {
                BookingSynchronization.SeatSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (userId == Guid.Empty)
            {
                return ServiceResult<BookingInfo>.Fail(ServiceErrorType.Unauthorized, "User identifier is required.");
            }

            Booking? booking = await bookingRepository.GetByIdAsync(bookingId, cancellationToken);
            if (booking is not null)
            {
                EnsureAccess(booking, userId, isAdmin, "The user is not allowed to view this booking.");

                return ServiceResult<BookingInfo>.Succeed(booking.ToInfo());
            }

            return ServiceResult<BookingInfo>.Fail(
                ServiceErrorType.NotFound,
                "Booking not found.");
        }

        /// <inheritdoc />
        public async Task<ServiceResult> CancelBookingAsync(Guid bookingId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return ServiceResult.Fail(ServiceErrorType.Unauthorized, "User identifier is required.");
            }

            SemaphoreSlim bookingSemaphore = BookingSynchronization.GetBookingSemaphore(bookingId);
            await bookingSemaphore.WaitAsync(cancellationToken);

            try
            {
                await BookingSynchronization.SeatSemaphore.WaitAsync(cancellationToken);

                try
                {
                    Booking? booking = await bookingRepository.GetByIdForUpdateAsync(bookingId, cancellationToken);
                    if (booking is null)
                    {
                        return ServiceResult.Fail(ServiceErrorType.NotFound, "Booking not found.");
                    }

                    EnsureAccess(booking, userId, isAdmin, "The user is not allowed to cancel this booking.");
                    if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
                    {
                        return ServiceResult.Fail(ServiceErrorType.Conflict, "Only an active booking can be cancelled.");
                    }

                    Event? @event = await eventRepository.GetCurrentByIdForUpdateAsync(booking.EventId, cancellationToken);
                    if (@event is null)
                    {
                        return ServiceResult.Fail(ServiceErrorType.NotFound, "Event not found.");
                    }

                    @event.ReleaseSeats();
                    booking.Cancel();
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("Booking {BookingId} cancelled by user {UserId}.", bookingId, userId);

                    return ServiceResult.Succeed();
                }
                finally
                {
                    BookingSynchronization.SeatSemaphore.Release();
                }
            }
            finally
            {
                bookingSemaphore.Release();
            }
        }

        /// <summary>Проверяет право владельца или администратора на доступ к брони.</summary>
        private void EnsureAccess(Booking booking, Guid userId, bool isAdmin, string errorMessage)
        {
            if (!isAdmin && booking.UserId != userId)
            {
                logger.LogWarning("Access to booking {BookingId} denied for user {UserId}.", booking.Id, userId);
                throw new BookingAccessDeniedException(errorMessage);
            }
        }
    }
}
