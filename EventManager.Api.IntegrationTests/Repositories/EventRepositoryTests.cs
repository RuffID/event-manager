using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Api.IntegrationTests.Infrastructure;
using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventManager.Api.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.NAME)]
public class EventRepositoryTests(PostgresFixture database)
{
    [Fact]
    public async Task Add_PersistsAllFieldsOnlyAfterUnitOfWorkCommit()
    {
        // Arrange
        await database.ResetAsync();
        Event created = TestData.CreateEvent();
        Assert.True(created.TryReserveSeats(2));
        await using AppDbContext write = database.CreateContext();
        EventRepository repository = new EventRepository(write);

        // Act
        repository.Add(created);

        // Assert
        await using (AppDbContext beforeCommit = database.CreateContext())
        {
            Assert.False(await beforeCommit.Events.AnyAsync(CancellationToken.None));
        }

        // Act
        int savedEntries = await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, savedEntries);
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertEvent(created, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOnlyRequestedEventWithoutTracking()
    {
        // Arrange
        await database.ResetAsync();
        Event expected = TestData.CreateEvent("Requested");
        Event other = TestData.CreateEvent("Other");
        await TestData.SeedAsync(database, [other, expected]);
        await using AppDbContext read = database.CreateContext();

        // Act
        Event? result = await new EventRepository(read).GetByIdAsync(expected.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        TestData.AssertEvent(expected, result);
        Assert.Empty(read.ChangeTracker.Entries());

        // Act
        result.UpdateDetails("Local change", null, result.StartAt, result.EndAt);
        int savedEntries = await new EfUnitOfWork(read).SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, savedEntries);
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertEvent(expected, await verify.Events.AsNoTracking().SingleAsync(item => item.Id == expected.Id, CancellationToken.None));
        TestData.AssertEvent(other, await verify.Events.AsNoTracking().SingleAsync(item => item.Id == other.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissingId()
    {
        // Arrange
        await database.ResetAsync();
        await TestData.SeedAsync(database, [TestData.CreateEvent()]);
        await using AppDbContext read = database.CreateContext();

        // Act
        Event? result = await new EventRepository(read).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_PersistsDetailsAndSeatsWithoutChangingOtherEvent()
    {
        // Arrange
        await database.ResetAsync();
        Event expected = TestData.CreateEvent("Before");
        Event other = TestData.CreateEvent("Other");
        await TestData.SeedAsync(database, [other, expected]);
        await using AppDbContext write = database.CreateContext();
        DateTime newStart = expected.StartAt.AddDays(1);
        DateTime newEnd = newStart.AddHours(4);

        // Act
        Event? result = await new EventRepository(write).GetByIdForUpdateAsync(expected.Id, CancellationToken.None);
        Assert.NotNull(result);
        result.UpdateDetails("After", "Changed description", newStart, newEnd);
        Assert.True(result.TryReserveSeats(3));
        await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);

        // Assert
        await using AppDbContext verify = database.CreateContext();

        Event saved = await verify.Events.AsNoTracking().SingleAsync(item => item.Id == expected.Id, CancellationToken.None);
        Assert.Equal("After", saved.Title);
        Assert.Equal("Changed description", saved.Description);
        Assert.Equal(newStart, saved.StartAt);
        Assert.Equal(newEnd, saved.EndAt);
        Assert.Equal(expected.TotalSeats, saved.TotalSeats);
        Assert.Equal(expected.TotalSeats - 3, saved.AvailableSeats);
        TestData.AssertEvent(other, await verify.Events.AsNoTracking().SingleAsync(item => item.Id == other.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ReturnsNullForMissingId()
    {
        // Arrange
        await database.ResetAsync();
        await TestData.SeedAsync(database, [TestData.CreateEvent()]);
        await using AppDbContext write = database.CreateContext();

        // Act
        Event? result = await new EventRepository(write).GetByIdForUpdateAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Empty(write.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetCurrentByIdForUpdateAsync_ReloadsTrackedEventAndRemainsUpdatable()
    {
        // Arrange
        await database.ResetAsync();
        Event expected = TestData.CreateEvent();
        Event other = TestData.CreateEvent("Other");
        await TestData.SeedAsync(database, [expected, other]);
        await using AppDbContext write = database.CreateContext();
        Event stale = await write.Events.SingleAsync(item => item.Id == expected.Id, CancellationToken.None);
        await using (AppDbContext external = database.CreateContext())
        {
            Event changed = await external.Events.SingleAsync(item => item.Id == expected.Id, CancellationToken.None);
            changed.UpdateDetails("External change", null, changed.StartAt, changed.EndAt);
            Assert.True(changed.TryReserveSeats(2));
            await new EfUnitOfWork(external).SaveChangesAsync(CancellationToken.None);
        }

        // Act
        Event? result = await new EventRepository(write).GetCurrentByIdForUpdateAsync(expected.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Same(stale, result);
        Assert.Equal("External change", result.Title);
        Assert.Null(result.Description);
        Assert.Equal(expected.TotalSeats - 2, result.AvailableSeats);
        Assert.Equal(EntityState.Unchanged, write.Entry(result).State);

        // Act
        Assert.True(result.TryReserveSeats());
        await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);

        // Assert
        await using AppDbContext verify = database.CreateContext();

        Event saved = await verify.Events.AsNoTracking().SingleAsync(item => item.Id == expected.Id, CancellationToken.None);
        Assert.Equal(expected.TotalSeats - 3, saved.AvailableSeats);
        Assert.Equal("External change", saved.Title);
        TestData.AssertEvent(other, await verify.Events.AsNoTracking().SingleAsync(item => item.Id == other.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentByIdForUpdateAsync_LoadsPreviouslyUntrackedEvent()
    {
        // Arrange
        await database.ResetAsync();
        Event expected = TestData.CreateEvent();
        await TestData.SeedAsync(database, [TestData.CreateEvent("Other"), expected]);
        await using AppDbContext read = database.CreateContext();

        // Act
        Event? result = await new EventRepository(read).GetCurrentByIdForUpdateAsync(expected.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        TestData.AssertEvent(expected, result);
        Assert.Equal(EntityState.Unchanged, read.Entry(result).State);
    }

    [Fact]
    public async Task GetCurrentByIdForUpdateAsync_ReturnsNullForMissingId()
    {
        // Arrange
        await database.ResetAsync();
        await TestData.SeedAsync(database, [TestData.CreateEvent()]);
        await using AppDbContext read = database.CreateContext();

        // Act
        Event? result = await new EventRepository(read).GetCurrentByIdForUpdateAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Empty(read.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetCurrentByIdForUpdateAsync_ReturnsNullForPreviouslyTrackedDeletedEvent()
    {
        // Arrange
        await database.ResetAsync();
        Event deleted = TestData.CreateEvent("Deleted");
        Event other = TestData.CreateEvent("Other");
        await TestData.SeedAsync(database, [deleted, other]);
        await using AppDbContext read = database.CreateContext();
        await read.Events.SingleAsync(item => item.Id == deleted.Id, CancellationToken.None);
        await using (AppDbContext external = database.CreateContext())
        {
            Event existing = await external.Events.SingleAsync(item => item.Id == deleted.Id, CancellationToken.None);
            external.Events.Remove(existing);
            await new EfUnitOfWork(external).SaveChangesAsync(CancellationToken.None);
        }

        // Act
        Event? result = await new EventRepository(read).GetCurrentByIdForUpdateAsync(deleted.Id, CancellationToken.None);

        // Assert
        Assert.Null(result);
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertEvent(other, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistsAsync_ReturnsWhetherRequestedIdExists(bool exists)
    {
        // Arrange
        await database.ResetAsync();
        Event expected = TestData.CreateEvent();
        await TestData.SeedAsync(database, [TestData.CreateEvent("Other"), expected]);
        Guid requestedId = Guid.NewGuid();
        if (exists)
        {
            requestedId = expected.Id;
        }

        await using AppDbContext read = database.CreateContext();

        // Act
        bool result = await new EventRepository(read).ExistsAsync(requestedId, CancellationToken.None);

        // Assert
        Assert.Equal(exists, result);
        Assert.Empty(read.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Remove_CommitsServerCascadeAndKeepsUnrelatedEventAndBookings()
    {
        // Arrange
        await database.ResetAsync();
        Event deleted = TestData.CreateEvent("Deleted");
        Event other = TestData.CreateEvent("Other");
        Booking first = new Booking(deleted.Id);
        Booking second = new Booking(deleted.Id);
        Booking unrelated = new Booking(other.Id);
        await TestData.SeedAsync(database, [deleted, other], first, second, unrelated);
        await using AppDbContext write = database.CreateContext();
        Event target = await write.Events.SingleAsync(item => item.Id == deleted.Id, CancellationToken.None);
        Assert.Empty(write.ChangeTracker.Entries<Booking>());
        EventRepository repository = new EventRepository(write);

        // Act
        repository.Remove(target);

        // Assert
        await using (AppDbContext beforeCommit = database.CreateContext())
        {
            Assert.True(await beforeCommit.Events.AnyAsync(item => item.Id == deleted.Id, CancellationToken.None));
            Assert.Equal(3, await beforeCommit.Bookings.CountAsync(CancellationToken.None));
        }

        // Act
        await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);

        // Assert
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertEvent(other, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
        TestData.AssertBooking(unrelated, await verify.Bookings.AsNoTracking().SingleAsync(CancellationToken.None));
        Assert.False(await verify.Events.AnyAsync(item => item.Id == deleted.Id, CancellationToken.None));
        Assert.False(await verify.Bookings.AnyAsync(item => item.EventId == deleted.Id, CancellationToken.None));
    }
}
