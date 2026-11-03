using EventManager.Application.Services;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManager.Api.Tests.Services;

public class BookingProcessingPortTests
{
    [Theory]
    [InlineData(true, BookingStatus.Confirmed)]
    [InlineData(false, BookingStatus.Rejected)]
    public async Task ProcessBooking_SavesExpectedTransitionWithCallerToken(bool eventExists, BookingStatus status)
    {
        // Arrange
        Event existing = CreateEvent();
        Booking booking = new Booking(existing.Id);
        RecordingBookingRepository bookings = new RecordingBookingRepository { Booking = booking };
        RecordingEventRepository events = new RecordingEventRepository();
        if (eventExists)
        {
            events.Event = existing;
        }

        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingProcessing processing = CreateProcessing(bookings, events, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        await processing.ProcessBookingAsync(booking.Id, cancellation.Token);

        // Assert
        Assert.Equal(status, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        Assert.Equal(("update", booking.Id, cancellation.Token), Assert.Single(bookings.Calls));
        Assert.Equal(("exists", existing.Id, cancellation.Token), Assert.Single(events.Calls));
        Assert.Equal(cancellation.Token, Assert.Single(work.SavedTokens));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(BookingStatus.Confirmed, false)]
    [InlineData(BookingStatus.Rejected, false)]
    [InlineData(null, true)]
    [InlineData(BookingStatus.Confirmed, true)]
    [InlineData(BookingStatus.Rejected, true)]
    public async Task CompletedOrMissingBooking_DoesNotSaveOrReturnSeat(BookingStatus? status, bool compensate)
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats(2));
        Booking booking = new Booking(existing.Id);
        RecordingBookingRepository bookings = new RecordingBookingRepository();
        if (status == BookingStatus.Confirmed)
        {
            booking.Confirm();
            bookings.Booking = booking;
        }
        else if (status == BookingStatus.Rejected)
        {
            booking.Reject();
            bookings.Booking = booking;
        }

        DateTime? processedAt = booking.ProcessedAt;
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingProcessing processing = CreateProcessing(bookings, events, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        if (compensate)
        {
            await processing.RejectBookingAfterFailureAsync(booking.Id);
        }
        else
        {
            await processing.ProcessBookingAsync(booking.Id, cancellation.Token);
        }

        // Assert
        Assert.Equal(processedAt, booking.ProcessedAt);
        if (status.HasValue)
        {
            Assert.Equal(status.Value, booking.Status);
        }

        Assert.Equal(0, existing.AvailableSeats);
        Assert.Empty(events.Calls);
        Assert.Empty(work.SavedTokens);
        Assert.Equal(booking.Id, Assert.Single(bookings.Calls).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compensation_RejectsAndSavesOnce_WithoutReturningAnotherBookingsSeat(bool eventExists)
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats(2));
        Booking booking = new Booking(existing.Id);
        RecordingBookingRepository bookings = new RecordingBookingRepository { Booking = booking };
        RecordingEventRepository events = new RecordingEventRepository();
        if (eventExists)
        {
            events.Event = existing;
        }

        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingProcessing processing = CreateProcessing(bookings, events, work);

        // Act
        await processing.RejectBookingAfterFailureAsync(booking.Id);

        // Assert
        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        Assert.Equal(("update", booking.Id, CancellationToken.None), Assert.Single(bookings.Calls));
        Assert.Equal(("update", existing.Id, CancellationToken.None), Assert.Single(events.Calls));
        Assert.Equal(CancellationToken.None, Assert.Single(work.SavedTokens));

        // Arrange
        DateTime? processedAt = booking.ProcessedAt;
        int availableSeats = existing.AvailableSeats;

        // Act
        await processing.RejectBookingAfterFailureAsync(booking.Id);

        // Assert
        Assert.Equal(processedAt, booking.ProcessedAt);
        Assert.Equal(availableSeats, existing.AvailableSeats);
        if (eventExists)
        {
            Assert.Equal(1, availableSeats);
        }
        else
        {
            Assert.Equal(0, availableSeats);
        }

        Assert.Single(events.Calls);
        Assert.Single(work.SavedTokens);
        Assert.Equal(2, bookings.Calls.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveFailure_IsPropagatedFromProcessingAndCompensation(bool compensate)
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats());
        Booking booking = new Booking(existing.Id);
        RecordingBookingRepository bookings = new RecordingBookingRepository { Booking = booking };
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        InvalidOperationException failure = new InvalidOperationException("Save failed.");
        RecordingUnitOfWork work = new RecordingUnitOfWork { SaveError = failure };
        BookingProcessing processing = CreateProcessing(bookings, events, work);

        // Act
        Func<Task> action;
        if (compensate)
        {
            action = () => processing.RejectBookingAfterFailureAsync(booking.Id);
        }
        else
        {
            action = () => processing.ProcessBookingAsync(booking.Id, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(action));
        Assert.Single(work.SavedTokens);
    }

    private static BookingProcessing CreateProcessing(RecordingBookingRepository bookings,
        RecordingEventRepository events, RecordingUnitOfWork work)
    {
        return new BookingProcessing(bookings, events, work, NullLogger<BookingProcessing>.Instance);
    }

    private static Event CreateEvent()
    {
        DateTime startAt = DateTime.UtcNow.AddDays(1);

        return Event.Create("Processing event", null, startAt, startAt.AddHours(2), 2);
    }
}
