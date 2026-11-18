using EventManager.Application.Abstractions.Persistence;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using EventManager.Domain.Exceptions;
using EventManager.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventManager.Api.Tests.Services;

public class BookingRulesTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    public async Task Create_LimitBoundaryCountsMixedActiveStatusesAndPreservesDataOnFailure(int activeCount)
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event existing = CreateEvent();
        Guid userId = Guid.NewGuid();
        List<Booking> bookings = [];
        for (int index = 0; index < activeCount; index++)
        {
            Assert.True(existing.TryReserveSeats());
            Booking booking = new Booking(existing.Id, userId);
            if (index % 2 == 0)
            {
                booking.Confirm();
            }

            bookings.Add(booking);
        }

        Booking cancelled = new Booking(existing.Id, userId);
        cancelled.Cancel();
        Booking rejected = new Booking(existing.Id, userId);
        rejected.Reject();
        bookings.AddRange([cancelled, rejected]);
        await database.SeedAsync(existing);
        database.Context.Bookings.AddRange(bookings);
        CancellationToken token = TestContext.Current.CancellationToken;
        await database.Context.SaveChangesAsync(token);
        database.Context.ChangeTracker.Clear();
        int seatsBefore = existing.AvailableSeats;
        ServiceResult<BookingInfo>? result = null;

        // Act
        Exception? error = await Record.ExceptionAsync(async () =>
        {
            result = await database.BookingService.CreateBookingAsync(existing.Id, userId, token);
        });

        // Assert
        int added = 0;
        if (activeCount < BookingService.MAX_ACTIVE_BOOKINGS)
        {
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(userId, Assert.IsType<BookingInfo>(result.Data).UserId);
            added = 1;
        }
        else
        {
            ActiveBookingLimitExceededException limitError = Assert.IsType<ActiveBookingLimitExceededException>(error);
            Assert.Contains(BookingService.MAX_ACTIVE_BOOKINGS.ToString(), limitError.Message);
            Assert.Null(result);
        }

        database.Context.ChangeTracker.Clear();
        Assert.Equal(bookings.Count + added, await database.Context.Bookings.CountAsync(token));
        Assert.Equal(seatsBefore - added, (await database.Context.Events.SingleAsync(token)).AvailableSeats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_ForeignBookingRequiresAdminAndPersistsOnlyAllowedChanges(bool isAdmin)
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event existing = CreateEvent();
        await database.SeedAsync(existing);
        Guid ownerId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        CancellationToken token = TestContext.Current.CancellationToken;
        ServiceResult<BookingInfo> created = await database.BookingService.CreateBookingAsync(existing.Id, ownerId, token);
        BookingInfo booking = Assert.IsType<BookingInfo>(created.Data);
        ServiceResult? result = null;

        // Act
        Exception? error = await Record.ExceptionAsync(async () =>
        {
            result = await database.BookingService.CancelBookingAsync(booking.Id, actorId, isAdmin, token);
        });

        // Assert
        BookingStatus expectedStatus = BookingStatus.Pending;
        int expectedSeats = existing.TotalSeats - 1;
        if (isAdmin)
        {
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.True(result.Success);
            expectedStatus = BookingStatus.Cancelled;
            expectedSeats = existing.TotalSeats;
        }
        else
        {
            Assert.IsType<BookingAccessDeniedException>(error);
            Assert.Null(result);
        }

        database.Context.ChangeTracker.Clear();
        Booking stored = await database.Context.Bookings.AsNoTracking().SingleAsync(token);
        Assert.Equal(ownerId, stored.UserId);
        Assert.Equal(expectedStatus, stored.Status);
        Assert.Equal(expectedSeats, (await database.Context.Events.AsNoTracking().SingleAsync(token)).AvailableSeats);
    }

    [Fact]
    public async Task Create_PastEventDoesNotReserveSeatOrAddBooking()
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event existing = CreateEvent();
        await database.SeedAsync(existing);
        Event stored = await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken);
        // Имитируем сохранённое событие, время начала которого уже прошло.
        database.Context.Entry(stored).Property(item => item.StartAt).CurrentValue = DateTime.UtcNow.AddHours(-1);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.Context.ChangeTracker.Clear();

        // Act
        Func<Task> action = () => database.BookingService.CreateBookingAsync(existing.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<EventAlreadyStartedException>(action);
        database.Context.ChangeTracker.Clear();
        Assert.Empty(await database.Context.Bookings.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(existing.TotalSeats, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
    }

    [Fact]
    public async Task Create_UserLimitAppliesAcrossEventsButDoesNotAffectAnotherUser()
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event first = CreateEvent();
        Event second = CreateEvent();
        await database.SeedAsync(first, second);
        Guid firstUser = Guid.NewGuid();
        Guid secondUser = Guid.NewGuid();
        foreach (int _ in Enumerable.Range(0, BookingService.MAX_ACTIVE_BOOKINGS))
        {
            ServiceResult<BookingInfo> created = await database.BookingService.CreateBookingAsync(first.Id, firstUser, TestContext.Current.CancellationToken);
            Assert.True(created.Success);
            Assert.Equal(firstUser, Assert.IsType<BookingInfo>(created.Data).UserId);
        }

        // Act
        ActiveBookingLimitExceededException error = await Assert.ThrowsAsync<ActiveBookingLimitExceededException>(
            () => database.BookingService.CreateBookingAsync(second.Id, firstUser, TestContext.Current.CancellationToken));
        ServiceResult<BookingInfo> otherUser = await database.BookingService.CreateBookingAsync(second.Id, secondUser, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(BookingService.MAX_ACTIVE_BOOKINGS.ToString(), error.Message);
        Assert.True(otherUser.Success);
        Assert.Equal(secondUser, Assert.IsType<BookingInfo>(otherUser.Data).UserId);
        database.Context.ChangeTracker.Clear();
        Assert.Equal(BookingService.MAX_ACTIVE_BOOKINGS, await database.Context.Bookings.CountAsync(item => item.UserId == firstUser, TestContext.Current.CancellationToken));
        Assert.Equal(second.TotalSeats - 1, (await database.Context.Events.SingleAsync(item => item.Id == second.Id, TestContext.Current.CancellationToken)).AvailableSeats);
    }

    [Theory]
    [InlineData(BookingStatus.Pending, 1)]
    [InlineData(BookingStatus.Confirmed, 1)]
    [InlineData(BookingStatus.Rejected, 0)]
    [InlineData(BookingStatus.Cancelled, 0)]
    public async Task CountActive_OnlyPendingAndConfirmedBelongingToUserAreIncluded(BookingStatus status, int expected)
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event existing = CreateEvent();
        await database.SeedAsync(existing);
        Guid userId = Guid.NewGuid();
        Booking booking = new Booking(existing.Id, userId);
        if (status == BookingStatus.Confirmed)
        {
            booking.Confirm();
        }
        else if (status == BookingStatus.Rejected)
        {
            booking.Reject();
        }
        else if (status == BookingStatus.Cancelled)
        {
            booking.Cancel();
        }

        database.Context.Bookings.AddRange(booking, new Booking(existing.Id, Guid.NewGuid()));
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        using IServiceScope scope = database.ServiceProvider.CreateScope();
        IBookingRepository repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        // Act
        int count = await repository.CountActiveByUserAsync(userId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, count);
    }

    [Fact]
    public async Task Cancel_ReleasesUserLimitAndSeatForNextBooking()
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event existing = CreateEvent();
        await database.SeedAsync(existing);
        Guid userId = Guid.NewGuid();
        BookingInfo? first = null;
        foreach (int _ in Enumerable.Range(0, BookingService.MAX_ACTIVE_BOOKINGS))
        {
            ServiceResult<BookingInfo> created = await database.BookingService.CreateBookingAsync(existing.Id, userId, TestContext.Current.CancellationToken);
            first = Assert.IsType<BookingInfo>(created.Data);
        }

        Assert.NotNull(first);

        // Act
        ServiceResult cancelled = await database.BookingService.CancelBookingAsync(first.Id, userId, false, TestContext.Current.CancellationToken);
        ServiceResult<BookingInfo> next = await database.BookingService.CreateBookingAsync(existing.Id, userId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(cancelled.Success);
        Assert.True(next.Success);
        Assert.NotEqual(first.Id, Assert.IsType<BookingInfo>(next.Data).Id);
        database.Context.ChangeTracker.Clear();
        Assert.Equal(BookingStatus.Cancelled, (await database.Context.Bookings.SingleAsync(item => item.Id == first.Id, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(existing.TotalSeats - BookingService.MAX_ACTIVE_BOOKINGS, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
    }

    [Fact]
    public async Task Create_ConcurrentRequestsForSameUserDoNotExceedLimit()
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event existing = CreateEvent();
        await database.SeedAsync(existing);
        Guid userId = Guid.NewGuid();
        const int REQUEST_COUNT = BookingService.MAX_ACTIVE_BOOKINGS + 3;

        // Act
        Task<bool>[] requests = Enumerable.Range(0, REQUEST_COUNT).Select(_ => Task.Run(async () =>
        {
            using IServiceScope scope = database.ServiceProvider.CreateScope();
            IBookingService service = scope.ServiceProvider.GetRequiredService<IBookingService>();
            try
            {
                ServiceResult<BookingInfo> result = await service.CreateBookingAsync(existing.Id, userId, TestContext.Current.CancellationToken);

                return result.Success;
            }
            catch (ActiveBookingLimitExceededException)
            {
                return false;
            }
        })).ToArray();
        bool[] outcomes = await Task.WhenAll(requests);

        // Assert
        Assert.Equal(BookingService.MAX_ACTIVE_BOOKINGS, outcomes.Count(success => success));
        database.Context.ChangeTracker.Clear();
        Assert.Equal(BookingService.MAX_ACTIVE_BOOKINGS, await database.Context.Bookings.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(existing.TotalSeats - BookingService.MAX_ACTIVE_BOOKINGS, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
    }

    [Fact]
    public async Task Cancel_StaleScopeDoesNotConfirmCancelledBookingOrReturnSeatTwice()
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        Event existing = CreateEvent();
        await database.SeedAsync(existing);
        Guid userId = Guid.NewGuid();
        ServiceResult<BookingInfo> created = await database.BookingService.CreateBookingAsync(existing.Id, userId, TestContext.Current.CancellationToken);
        BookingInfo booking = Assert.IsType<BookingInfo>(created.Data);
        using IServiceScope staleScope = database.ServiceProvider.CreateScope();
        AppDbContext staleContext = staleScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Booking stale = await staleContext.Bookings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(BookingStatus.Pending, stale.Status);

        // Act
        ServiceResult cancelled = await database.BookingService.CancelBookingAsync(booking.Id, userId, false, TestContext.Current.CancellationToken);
        BookingProcessing processing = staleScope.ServiceProvider.GetRequiredService<BookingProcessing>();
        await processing.ProcessBookingAsync(booking.Id, TestContext.Current.CancellationToken);
        IBookingService staleService = staleScope.ServiceProvider.GetRequiredService<IBookingService>();
        ServiceResult repeated = await staleService.CancelBookingAsync(booking.Id, userId, false, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(cancelled.Success);
        Assert.False(repeated.Success);
        Assert.Equal(ServiceErrorType.Conflict, Assert.IsType<ServiceError>(repeated.Error).Type);
        database.Context.ChangeTracker.Clear();
        Booking saved = await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(BookingStatus.Cancelled, saved.Status);
        Assert.Null(saved.ProcessedAt);
        Assert.Equal(existing.TotalSeats, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
    }

    private static Event CreateEvent()
    {
        DateTime start = DateTime.UtcNow.AddDays(1);

        return Event.Create("Booking rules", null, start, start.AddHours(2), 30);
    }
}
