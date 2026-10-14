using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Api.IntegrationTests.Infrastructure;
using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace EventManager.Api.IntegrationTests.Migrations;

[Collection(PostgreSqlCollection.NAME)]
public class ConstraintTests(PostgresFixture database)
{
    [Fact]
    public async Task BookingAdd_UnknownEventFailsWithExactForeignKeyViolation()
    {
        // Arrange
        await database.ResetAsync();
        Event other = TestData.CreateEvent("Existing event");
        await TestData.SeedAsync(database, [other]);
        Guid missingId = Guid.NewGuid();
        Booking invalid = new Booking(missingId);
        await using AppDbContext write = database.CreateContext();

        Assert.False(await write.Events.AnyAsync(item => item.Id == missingId, CancellationToken.None));

        // Act
        new BookingRepository(write).Add(invalid);
        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None));

        // Assert
        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
        Assert.Equal("FK_bookings_events_event_id", postgres.ConstraintName);
        await using AppDbContext verify = database.CreateContext();

        Assert.Empty(await verify.Bookings.AsNoTracking().ToListAsync(CancellationToken.None));
        TestData.AssertEvent(other, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnitOfWork_ForeignKeyFailureDoesNotPartiallySaveEventAndBookings()
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        Booking valid = new Booking(parent.Id);
        Booking invalid = new Booking(Guid.NewGuid());
        await using AppDbContext write = database.CreateContext();

        Assert.False(await write.Events.AnyAsync(item => item.Id == invalid.EventId, CancellationToken.None));

        // Act
        new EventRepository(write).Add(parent);
        BookingRepository bookings = new BookingRepository(write);
        bookings.Add(valid);
        bookings.Add(invalid);
        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None));

        // Assert
        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
        Assert.Equal("FK_bookings_events_event_id", postgres.ConstraintName);
        await using AppDbContext verify = database.CreateContext();

        Assert.False(await verify.Events.AnyAsync(CancellationToken.None));
        Assert.False(await verify.Bookings.AnyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EventAdd_DuplicateIdFailsWithExactPrimaryKeyViolation()
    {
        // Arrange
        await database.ResetAsync();
        Event existing = TestData.CreateEvent("Existing");
        await TestData.SeedAsync(database, [existing]);
        Event duplicate = new Event(existing.Id, "Duplicate", null, existing.StartAt, existing.EndAt, 5);
        await using AppDbContext write = database.CreateContext();

        // Act
        new EventRepository(write).Add(duplicate);
        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None));

        // Assert
        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("PK_events", postgres.ConstraintName);
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertEvent(existing, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task BookingAdd_DuplicateIdFailsWithExactPrimaryKeyViolation()
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        Booking existing = new Booking(parent.Id);
        await TestData.SeedAsync(database, [parent], existing);
        Booking duplicate = new Booking(parent.Id);
        await using AppDbContext write = database.CreateContext();

        write.Entry(duplicate).Property(item => item.Id).CurrentValue = existing.Id;

        // Act
        new BookingRepository(write).Add(duplicate);
        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None));

        // Assert
        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("PK_bookings", postgres.ConstraintName);
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertBooking(existing, await verify.Bookings.AsNoTracking().SingleAsync(CancellationToken.None));
        TestData.AssertEvent(parent, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("Title", Event.MAX_TITLE_LENGTH)]
    [InlineData("Description", Event.MAX_DESCRIPTION_LENGTH)]
    public async Task EventAdd_OverlongTextFailsWithStringDataRightTruncation(string property, int limit)
    {
        // Arrange
        await database.ResetAsync();
        Event invalid = TestData.CreateEvent();
        await using AppDbContext write = database.CreateContext();

        write.Entry(invalid).Property(property).CurrentValue = new string('x', limit + 1);

        // Act
        new EventRepository(write).Add(invalid);
        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None));

        // Assert
        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, postgres.SqlState);
        await using AppDbContext verify = database.CreateContext();

        Assert.False(await verify.Events.AnyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EventAdd_NullTitleFailsWithExactNotNullViolation()
    {
        // Arrange
        await database.ResetAsync();
        Event invalid = TestData.CreateEvent();
        await using AppDbContext write = database.CreateContext();

        write.Entry(invalid).Property(item => item.Title).CurrentValue = null!;

        // Act
        new EventRepository(write).Add(invalid);
        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None));

        // Assert
        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.NotNullViolation, postgres.SqlState);
        Assert.Equal("events", postgres.TableName);
        Assert.Equal("title", postgres.ColumnName);
        await using AppDbContext verify = database.CreateContext();

        Assert.False(await verify.Events.AnyAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("start_at")]
    [InlineData("end_at")]
    [InlineData("total_seats")]
    [InlineData("available_seats")]
    public async Task Events_RejectNullInEveryRequiredValueColumn(string column)
    {
        // Arrange
        await database.ResetAsync();
        Guid? id = Guid.NewGuid();
        DateTime? start = DateTime.UtcNow.Date.AddDays(10);
        DateTime? end = start.Value.AddHours(2);
        int? totalSeats = 10;
        int? availableSeats = 10;
        if (column == "id")
        {
            id = null;
        }
        else if (column == "start_at")
        {
            start = null;
        }
        else if (column == "end_at")
        {
            end = null;
        }
        else if (column == "total_seats")
        {
            totalSeats = null;
        }
        else if (column == "available_seats")
        {
            availableSeats = null;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        await using AppDbContext write = database.CreateContext();

        // Act
        // Параметризованный INSERT только для недостижимых через модель отрицательных сценариев.
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => write.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO events (id, title, start_at, end_at, total_seats, available_seats)
            VALUES ({id}, {"Valid title"}, {start}, {end}, {totalSeats}, {availableSeats})
            """, CancellationToken.None));

        // Assert
        Assert.Equal(PostgresErrorCodes.NotNullViolation, exception.SqlState);
        Assert.Equal("events", exception.TableName);
        Assert.Equal(column, exception.ColumnName);
        await using AppDbContext verify = database.CreateContext();

        Assert.False(await verify.Events.AnyAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("event_id")]
    [InlineData("status")]
    [InlineData("created_at")]
    public async Task Bookings_RejectNullInEveryRequiredColumn(string column)
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        await TestData.SeedAsync(database, [parent]);
        Guid? id = Guid.NewGuid();
        Guid? eventId = parent.Id;
        string? status = nameof(BookingStatus.Pending);
        DateTime? createdAt = DateTime.UtcNow;
        if (column == "id")
        {
            id = null;
        }
        else if (column == "event_id")
        {
            eventId = null;
        }
        else if (column == "status")
        {
            status = null;
        }
        else if (column == "created_at")
        {
            createdAt = null;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        await using AppDbContext write = database.CreateContext();

        // Act
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => write.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO bookings (id, event_id, status, created_at)
            VALUES ({id}, {eventId}, {status}, {createdAt})
            """, CancellationToken.None));

        // Assert
        Assert.Equal(PostgresErrorCodes.NotNullViolation, exception.SqlState);
        Assert.Equal("bookings", exception.TableName);
        Assert.Equal(column, exception.ColumnName);
        await using AppDbContext verify = database.CreateContext();

        Assert.False(await verify.Bookings.AnyAsync(CancellationToken.None));
        TestData.AssertEvent(parent, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Bookings_RejectStatusLongerThanExistingColumnLimit()
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent();
        await TestData.SeedAsync(database, [parent]);
        Guid id = Guid.NewGuid();
        string status = new string('x', 21);
        DateTime createdAt = DateTime.UtcNow;
        await using AppDbContext write = database.CreateContext();

        // Act
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => write.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO bookings (id, event_id, status, created_at)
            VALUES ({id}, {parent.Id}, {status}, {createdAt})
            """, CancellationToken.None));

        // Assert
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, exception.SqlState);
        await using AppDbContext verify = database.CreateContext();

        Assert.False(await verify.Bookings.AnyAsync(CancellationToken.None));
        TestData.AssertEvent(parent, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventAdd_AcceptsMaximumTextLengthsAndNullableDescription(bool useNullDescription)
    {
        // Arrange
        await database.ResetAsync();
        string? description = new string('d', Event.MAX_DESCRIPTION_LENGTH);
        if (useNullDescription)
        {
            description = null;
        }

        Event valid = TestData.CreateEvent(new string('t', Event.MAX_TITLE_LENGTH), description: description);
        await using AppDbContext write = database.CreateContext();

        // Act
        new EventRepository(write).Add(valid);
        await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);

        // Assert
        await using AppDbContext verify = database.CreateContext();

        TestData.AssertEvent(valid, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
    }
}
