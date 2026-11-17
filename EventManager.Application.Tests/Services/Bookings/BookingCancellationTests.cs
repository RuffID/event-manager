using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using EventManager.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManager.Application.Tests.Services.Bookings;

public class BookingCancellationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Cancel_OwnerOrAdminReturnsSeatOnceAndKeepsProcessingTime(bool isAdmin, bool confirmed)
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats());
        Booking booking = new Booking(existing.Id, Guid.NewGuid());
        if (confirmed)
        {
            booking.Confirm();
        }

        Guid actorId = booking.UserId;
        if (isAdmin)
        {
            actorId = Guid.NewGuid();
        }

        DateTime? processedAt = booking.ProcessedAt;
        RecordingBookingRepository bookings = new RecordingBookingRepository { Booking = booking };
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        RecordingLogger<BookingService> logger = new RecordingLogger<BookingService>();
        BookingService service = new BookingService(events, bookings, work, logger);
        CancellationToken token = TestContext.Current.CancellationToken;

        // Act
        ServiceResult result = await service.CancelBookingAsync(booking.Id, actorId, isAdmin, token);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(processedAt, booking.ProcessedAt);
        Assert.Equal(existing.TotalSeats, existing.AvailableSeats);
        Assert.Equal(token, Assert.Single(work.SavedTokens));
        Assert.Equal(("current", existing.Id, token), Assert.Single(events.Calls));
        Assert.Contains(actorId.ToString(), Assert.Single(logger.Entries).Message);

        // Act
        ServiceResult repeated = await service.CancelBookingAsync(booking.Id, actorId, isAdmin, token);
        BookingProcessing processing = new BookingProcessing(bookings, events, work, NullLogger<BookingProcessing>.Instance);
        await processing.ProcessBookingAsync(booking.Id, token);
        await processing.RejectBookingAfterFailureAsync(booking.Id);

        // Assert
        Assert.False(repeated.Success);
        Assert.Equal(ServiceErrorType.Conflict, Assert.IsType<ServiceError>(repeated.Error).Type);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(existing.TotalSeats, existing.AvailableSeats);
        Assert.Single(work.SavedTokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Access_ForeignUserCannotReadOrCancelBooking(bool cancel)
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats());
        Booking booking = new Booking(existing.Id, Guid.NewGuid());
        RecordingBookingRepository bookings = new RecordingBookingRepository { Booking = booking };
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        RecordingLogger<BookingService> logger = new RecordingLogger<BookingService>();
        BookingService service = new BookingService(events, bookings, work, logger);
        Guid actorId = Guid.NewGuid();
        Func<Task> action;
        if (cancel)
        {
            action = () => service.CancelBookingAsync(booking.Id, actorId, false, TestContext.Current.CancellationToken);
        }
        else
        {
            action = () => service.GetBookingByIdAsync(booking.Id, actorId, false, TestContext.Current.CancellationToken);
        }

        // Act
        Exception? error = await Record.ExceptionAsync(action);

        // Assert
        Assert.IsType<BookingAccessDeniedException>(error);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(1, existing.AvailableSeats);
        Assert.Empty(events.Calls);
        Assert.Empty(work.SavedTokens);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
        Assert.Contains(actorId.ToString(), Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public async Task Cancel_RejectedBookingDoesNotReturnSeatOrSave()
    {
        // Arrange
        Event existing = CreateEvent();
        Booking booking = new Booking(existing.Id, Guid.NewGuid());
        booking.Reject();
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingBookingRepository bookings = new RecordingBookingRepository { Booking = booking };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingService service = new BookingService(events, bookings, work, NullLogger<BookingService>.Instance);

        // Act
        ServiceResult result = await service.CancelBookingAsync(booking.Id, booking.UserId, false, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, Assert.IsType<ServiceError>(result.Error).Type);
        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(existing.TotalSeats, existing.AvailableSeats);
        Assert.Empty(events.Calls);
        Assert.Empty(work.SavedTokens);
    }

    [Fact]
    public async Task Cancel_SaveFailureDoesNotLogSuccess()
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats());
        Booking booking = new Booking(existing.Id, Guid.NewGuid());
        InvalidOperationException failure = new InvalidOperationException("Save failed.");
        RecordingUnitOfWork work = new RecordingUnitOfWork { SaveError = failure };
        RecordingLogger<BookingService> logger = new RecordingLogger<BookingService>();
        BookingService service = new BookingService(
            new RecordingEventRepository { Event = existing },
            new RecordingBookingRepository { Booking = booking }, work, logger);

        // Act
        Func<Task> action = () => service.CancelBookingAsync(booking.Id, booking.UserId, false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(action));
        Assert.Single(work.SavedTokens);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_MissingBookingOrMissingActorDoesNotSave(bool missingActor)
    {
        // Arrange
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        RecordingBookingRepository bookings = new RecordingBookingRepository();
        BookingService service = new BookingService(new RecordingEventRepository(), bookings, work, NullLogger<BookingService>.Instance);
        Guid userId = Guid.NewGuid();
        ServiceErrorType expected = ServiceErrorType.NotFound;
        if (missingActor)
        {
            userId = Guid.Empty;
            expected = ServiceErrorType.Unauthorized;
        }

        // Act
        ServiceResult result = await service.CancelBookingAsync(Guid.NewGuid(), userId, false, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(expected, Assert.IsType<ServiceError>(result.Error).Type);
        Assert.Empty(work.SavedTokens);
    }

    private static Event CreateEvent()
    {
        DateTime startAt = DateTime.UtcNow.AddDays(1);

        return Event.Create("Cancellation event", null, startAt, startAt.AddHours(2), 2);
    }
}
