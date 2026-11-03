using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using EventManager.Domain.Exceptions;
using Xunit;

namespace EventManager.Api.Tests.Services;

public class BookingServicePortTests
{
    [Fact]
    public async Task CreateBooking_ReservesSeatAddsPendingBookingAndSavesWithCallerToken()
    {
        // Arrange
        Event existing = CreateEvent();
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingBookingRepository bookings = new RecordingBookingRepository();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingService service = new BookingService(events, bookings, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        ServiceResult<BookingInfo> result = await service.CreateBookingAsync(existing.Id, cancellation.Token);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Booking added = Assert.IsType<Booking>(bookings.Added);
        BookingInfo dto = Assert.IsType<BookingInfo>(result.Data);
        Assert.Equal(added.Id, dto.Id);
        Assert.Equal(existing.Id, dto.EventId);
        Assert.Equal(BookingStatus.Pending, dto.Status);
        Assert.Equal(added.CreatedAt, dto.CreatedAt);
        Assert.Null(dto.ProcessedAt);
        Assert.Equal(1, existing.AvailableSeats);
        Assert.Equal(("current", existing.Id, cancellation.Token), Assert.Single(events.Calls));
        Assert.Equal(cancellation.Token, Assert.Single(work.SavedTokens));
    }

    [Fact]
    public async Task CreateBooking_MissingEventDoesNotAddOrSave()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        RecordingEventRepository events = new RecordingEventRepository();
        RecordingBookingRepository bookings = new RecordingBookingRepository();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingService service = new BookingService(events, bookings, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        ServiceResult<BookingInfo> result = await service.CreateBookingAsync(id, cancellation.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.NotFound, Assert.IsType<ServiceError>(result.Error).Type);
        Assert.Null(bookings.Added);
        Assert.Empty(work.SavedTokens);
        Assert.Equal(("current", id, cancellation.Token), Assert.Single(events.Calls));
    }

    [Fact]
    public async Task CreateBooking_FullEventDoesNotAddOrSave()
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats(2));
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingBookingRepository bookings = new RecordingBookingRepository();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingService service = new BookingService(events, bookings, work);

        // Act
        Func<Task> action = () => service.CreateBookingAsync(existing.Id, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<NoAvailableSeatsException>(action);
        Assert.Equal(0, existing.AvailableSeats);
        Assert.Null(bookings.Added);
        Assert.Empty(work.SavedTokens);
    }

    [Fact]
    public async Task CreateBooking_SaveFailurePropagatesAndReleasesSemaphore()
    {
        // Arrange
        Event existing = CreateEvent();
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingBookingRepository bookings = new RecordingBookingRepository();
        InvalidOperationException failure = new InvalidOperationException("Save failed.");
        RecordingUnitOfWork work = new RecordingUnitOfWork { SaveError = failure };
        BookingService service = new BookingService(events, bookings, work);
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        Func<Task> action = () => service.CreateBookingAsync(existing.Id, timeout.Token);

        // Assert
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(action));
        Assert.Equal(timeout.Token, Assert.Single(work.SavedTokens));
        Assert.NotNull(bookings.Added);

        // Arrange
        // После отказа следующий запрос использует независимо загруженное состояние.
        events.Event = CreateEvent();
        work.SaveError = null;

        // Act
        ServiceResult<BookingInfo> next = await service.CreateBookingAsync(events.Event.Id, timeout.Token);

        // Assert
        Assert.True(next.Success);
        Assert.Equal(events.Event.Id, Assert.IsType<BookingInfo>(next.Data).EventId);
        Assert.Equal(2, work.SavedTokens.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetBooking_ForwardsIdAndTokenWithoutSaving(bool exists)
    {
        // Arrange
        Booking expected = new Booking(Guid.NewGuid());
        RecordingBookingRepository bookings = new RecordingBookingRepository();
        if (exists)
        {
            expected.Confirm();
            bookings.Booking = expected;
        }

        RecordingUnitOfWork work = new RecordingUnitOfWork();
        BookingService service = new BookingService(new RecordingEventRepository(), bookings, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        ServiceResult<BookingInfo> result = await service.GetBookingByIdAsync(expected.Id, cancellation.Token);

        // Assert
        Assert.Equal(exists, result.Success);
        if (exists)
        {
            BookingInfo dto = Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(expected.Id, dto.Id);
            Assert.Equal(expected.EventId, dto.EventId);
            Assert.Equal(BookingStatus.Confirmed, dto.Status);
            Assert.Equal(expected.ProcessedAt, dto.ProcessedAt);
            Assert.Null(result.Error);
        }
        else
        {
            Assert.Null(result.Data);
            Assert.Equal(ServiceErrorType.NotFound, Assert.IsType<ServiceError>(result.Error).Type);
        }

        Assert.Equal(("read", expected.Id, cancellation.Token), Assert.Single(bookings.Calls));
        Assert.Empty(work.SavedTokens);
    }

    private static Event CreateEvent()
    {
        DateTime startAt = DateTime.UtcNow.AddDays(1);

        return Event.Create("Booking event", null, startAt, startAt.AddHours(2), 2);
    }
}
