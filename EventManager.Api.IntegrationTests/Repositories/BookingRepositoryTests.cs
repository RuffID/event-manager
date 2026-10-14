using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Api.IntegrationTests.Infrastructure;
using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventManager.Api.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.NAME)]
public class BookingRepositoryTests(PostgresFixture database)
{
    [Fact]
    public async Task Add_PersistsBookingAndEventOnlyAfterSharedUnitOfWorkCommit()
    {
        // Arrange
        await database.ResetAsync();
        Event createdEvent = TestData.CreateEvent();
        Booking createdBooking = new Booking(createdEvent.Id);
        await using AppDbContext write = database.CreateContext();
        EventRepository events = new EventRepository(write);
        BookingRepository bookings = new BookingRepository(write);

        // Act
        events.Add(createdEvent);
        bookings.Add(createdBooking);

        // Assert
        await using (AppDbContext beforeCommit = database.CreateContext())
        {
            Assert.False(await beforeCommit.Events.AnyAsync(CancellationToken.None));
            Assert.False(await beforeCommit.Bookings.AnyAsync(CancellationToken.None));
        }

        // Act
        int savedEntries = await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, savedEntries);
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertEvent(createdEvent, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
        TestData.AssertBooking(createdBooking, await verify.Bookings.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task GetByIdAsync_ReturnsOnlyRequestedBookingWithoutTracking(BookingStatus status)
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        Event otherEvent = TestData.CreateEvent("Other");
        Booking expected = new Booking(parent.Id);
        CompleteBooking(expected, status);
        Booking sibling = new Booking(parent.Id);
        Booking unrelated = new Booking(otherEvent.Id);
        await TestData.SeedAsync(database, [parent, otherEvent], unrelated, sibling, expected);
        await using AppDbContext read = database.CreateContext();

        // Act
        Booking? result = await new BookingRepository(read).GetByIdAsync(expected.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        TestData.AssertBooking(expected, result);
        Assert.Empty(read.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotPersistLocalStatusChange()
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        Booking expected = new Booking(parent.Id);
        await TestData.SeedAsync(database, [parent], expected);
        await using AppDbContext read = database.CreateContext();

        // Act
        Booking? result = await new BookingRepository(read).GetByIdAsync(expected.Id, CancellationToken.None);
        Assert.NotNull(result);
        result.Confirm();
        int savedEntries = await new EfUnitOfWork(read).SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, savedEntries);
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertBooking(expected, await verify.Bookings.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissingId()
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        await TestData.SeedAsync(database, [parent], new Booking(parent.Id));
        await using AppDbContext read = database.CreateContext();

        // Act
        Booking? result = await new BookingRepository(read).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task GetByIdForUpdateAsync_PersistsStatusAndProcessingTimeAndKeepsOtherRows(BookingStatus status)
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        Booking expected = new Booking(parent.Id);
        Booking other = new Booking(parent.Id);
        await TestData.SeedAsync(database, [parent], other, expected);
        await using AppDbContext write = database.CreateContext();

        // Act
        Booking? result = await new BookingRepository(write).GetByIdForUpdateAsync(expected.Id, CancellationToken.None);
        Assert.NotNull(result);
        CompleteBooking(result, status);
        await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);

        // Assert
        await using AppDbContext verify = database.CreateContext();

        Booking saved = await verify.Bookings.AsNoTracking().SingleAsync(item => item.Id == expected.Id, CancellationToken.None);
        TestData.AssertBooking(result, saved);
        Assert.Equal(status, saved.Status);
        Assert.NotNull(saved.ProcessedAt);
        TestData.AssertBooking(other, await verify.Bookings.AsNoTracking().SingleAsync(item => item.Id == other.Id, CancellationToken.None));
        TestData.AssertEvent(parent, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ReturnsNullForMissingId()
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        await TestData.SeedAsync(database, [parent], new Booking(parent.Id));
        await using AppDbContext write = database.CreateContext();

        // Act
        Booking? result = await new BookingRepository(write).GetByIdForUpdateAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Empty(write.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetPendingIdsAsync_ReturnsOnlyMaterializedPendingIdsAcrossEvents()
    {
        // Arrange
        await database.ResetAsync();
        Event firstEvent = TestData.CreateEvent("First");
        Event secondEvent = TestData.CreateEvent("Second");
        Booking first = new Booking(firstEvent.Id);
        Booking second = new Booking(secondEvent.Id);
        Booking confirmed = new Booking(firstEvent.Id);
        Booking rejected = new Booking(secondEvent.Id);
        confirmed.Confirm();
        rejected.Reject();
        await TestData.SeedAsync(database, [firstEvent, secondEvent], confirmed, first, rejected, second);
        IReadOnlyList<Guid> result;
        int trackedEntries;

        // Act
        await using (AppDbContext read = database.CreateContext())
        {
            result = await new BookingRepository(read).GetPendingIdsAsync(CancellationToken.None);
            trackedEntries = read.ChangeTracker.Entries().Count();
        }

        // Assert
        // Порядок Pending не входит в контракт; проверяется точный состав после освобождения контекста.
        Assert.Equal(0, trackedEntries);
        Assert.Equal(2, result.Count);
        Assert.Contains(first.Id, result);
        Assert.Contains(second.Id, result);
        Assert.DoesNotContain(confirmed.Id, result);
        Assert.DoesNotContain(rejected.Id, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetPendingIdsAsync_ReturnsEmptyWhenNoPendingBookings(bool seedCompleted)
    {
        // Arrange
        await database.ResetAsync();
        if (seedCompleted)
        {
            Event parent = TestData.CreateEvent();
            Booking confirmed = new Booking(parent.Id);
            Booking rejected = new Booking(parent.Id);
            confirmed.Confirm();
            rejected.Reject();
            await TestData.SeedAsync(database, [parent], confirmed, rejected);
        }

        await using AppDbContext read = database.CreateContext();

        // Act
        IReadOnlyList<Guid> result = await new BookingRepository(read).GetPendingIdsAsync(CancellationToken.None);

        // Assert
        Assert.Empty(result);
        Assert.Empty(read.ChangeTracker.Entries());
    }

    private static void CompleteBooking(Booking booking, BookingStatus status)
    {
        if (status == BookingStatus.Confirmed)
        {
            booking.Confirm();
        }
        else if (status == BookingStatus.Rejected)
        {
            booking.Reject();
        }
        else if (status != BookingStatus.Pending)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
