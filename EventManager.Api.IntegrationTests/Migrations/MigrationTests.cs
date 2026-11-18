using EventManager.Infrastructure.DataAccess;
using EventManager.Infrastructure.DataAccess.Repositories;
using EventManager.Api.IntegrationTests.Infrastructure;
using EventManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace EventManager.Api.IntegrationTests.Migrations;

[Collection(PostgreSqlCollection.NAME)]
public class MigrationTests(PostgresFixture database)
{
    private const string INITIAL_MIGRATION = "20261014122528_InitialCreate";
    private const string RENAME_MIGRATION = "20261014145755_RenameColumnsToSnakeCase";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MigrateAsync_RenamesInitialSchemaColumnsAndPreservesLinkedData(bool confirmed)
    {
        // Arrange
        CancellationToken token = TestContext.Current.CancellationToken;
        await database.DeleteDatabaseAsync();
        Event parent = TestData.CreateEvent("Старое событие");
        Assert.True(parent.TryReserveSeats());
        Booking booking = new Booking(parent.Id, Guid.NewGuid());
        if (confirmed)
        {
            booking.Confirm();
        }
        else
        {
            parent.UpdateDetails(parent.Title, null, parent.StartAt, parent.EndAt);
        }

        await using AppDbContext migrate = database.CreateContext();

        IMigrator migrator = migrate.GetService<IMigrator>();
        Assert.Equal(new[] { INITIAL_MIGRATION, RENAME_MIGRATION }, migrate.Database.GetMigrations().Take(2));
        await migrator.MigrateAsync(INITIAL_MIGRATION, token);
        Assert.Equal(new[] { INITIAL_MIGRATION }, await migrate.Database.GetAppliedMigrationsAsync(token));

        // Текущая EF-модель ожидает новые имена; старые данные записываются в фактическую InitialCreate-схему.
        int insertedEvents = await migrate.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO events (id, "Title", "Description", "StartAt", "EndAt", "TotalSeats", "AvailableSeats")
            VALUES ({parent.Id}, {parent.Title}, {parent.Description}, {parent.StartAt}, {parent.EndAt},
                    {parent.TotalSeats}, {parent.AvailableSeats})
            """, token);
        int insertedBookings = await migrate.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO bookings ("Id", event_id, "Status", "CreatedAt", "ProcessedAt")
            VALUES ({booking.Id}, {booking.EventId}, {booking.Status.ToString()}, {booking.CreatedAt}, {booking.ProcessedAt})
            """, token);
        Assert.Equal(1, insertedEvents);
        Assert.Equal(1, insertedBookings);

        // Act
        await migrator.MigrateAsync(RENAME_MIGRATION, token);

        // Assert
        await using AppDbContext verify = database.CreateContext();

        Assert.Equal(new[] { INITIAL_MIGRATION, RENAME_MIGRATION }, await verify.Database.GetAppliedMigrationsAsync(token));
        Assert.EndsWith("_AddUsersAndBookingOwnership", Assert.Single(await verify.Database.GetPendingMigrationsAsync(token)));
        Event savedEvent = await verify.Events.AsNoTracking().SingleAsync(token);
        var savedBooking = await verify.Bookings.AsNoTracking()
            .Select(item => new { item.Id, item.EventId, item.Status, item.CreatedAt, item.ProcessedAt })
            .SingleAsync(token);
        TestData.AssertEvent(parent, savedEvent);
        Assert.Equal(booking.Id, savedBooking.Id);
        Assert.Equal(booking.EventId, savedBooking.EventId);
        Assert.Equal(booking.Status, savedBooking.Status);
        Assert.InRange(savedBooking.CreatedAt, booking.CreatedAt.AddMilliseconds(-1), booking.CreatedAt.AddMilliseconds(1));
        if (booking.ProcessedAt.HasValue)
        {
            Assert.NotNull(savedBooking.ProcessedAt);
            Assert.InRange(savedBooking.ProcessedAt.Value, booking.ProcessedAt.Value.AddMilliseconds(-1), booking.ProcessedAt.Value.AddMilliseconds(1));
        }
        else
        {
            Assert.Null(savedBooking.ProcessedAt);
        }
    }

    [Theory]
    [InlineData("events", "id,title,description,start_at,end_at,total_seats,available_seats")]
    [InlineData("bookings", "id,event_id,status,created_at,processed_at,user_id")]
    [InlineData("users", "id,login,password_hash,role")]
    public async Task MigrateAsync_CreatesExactPhysicalSnakeCaseColumns(string table, string expectedColumns)
    {
        // Arrange
        await database.ResetAsync();
        CancellationToken token = TestContext.Current.CancellationToken;
        await using AppDbContext context = database.CreateContext();
        string connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Строка подключения одноразовой БД отсутствует.");
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection connection = await source.OpenConnectionAsync(token);
        await using NpgsqlCommand command = new NpgsqlCommand(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            ORDER BY ordinal_position
            """, connection);
        command.Parameters.AddWithValue("table", table);
        List<string> columns = [];

        // Act
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                columns.Add(reader.GetString(0));
            }
        }

        // Assert
        Assert.Equal(expectedColumns.Split(','), columns);
    }

    [Fact]
    public async Task MigrateAsync_CreatesUsableSchemaFromEmptyDatabaseAndAppliesEntireAssemblyChain()
    {
        // Arrange
        await database.DeleteDatabaseAsync();
        await using AppDbContext migrate = database.CreateContext();

        string[] expected = migrate.Database.GetMigrations().ToArray();
        Assert.NotEmpty(expected);
        Event parent = TestData.CreateEvent("Migration schema");
        Booking booking = new Booking(parent.Id, Guid.NewGuid());

        // Act
        await migrate.Database.MigrateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expected, (await migrate.Database.GetAppliedMigrationsAsync(CancellationToken.None)).ToArray());
        Assert.Empty(await migrate.Database.GetPendingMigrationsAsync(CancellationToken.None));
        await using (AppDbContext write = database.CreateContext())
        {
            new EventRepository(write).Add(parent);
            new UserRepository(write).Add(TestData.CreateUser(booking.UserId));
            new BookingRepository(write).Add(booking);
            await new EfUnitOfWork(write).SaveChangesAsync(CancellationToken.None);
        }

        await using AppDbContext verify = database.CreateContext();

        Event savedEvent = await verify.Events.AsNoTracking().Include(item => item.Bookings).SingleAsync(CancellationToken.None);
        Booking savedBooking = await verify.Bookings.AsNoTracking().Include(item => item.Event).SingleAsync(CancellationToken.None);
        TestData.AssertEvent(parent, savedEvent);
        TestData.AssertBooking(booking, Assert.Single(savedEvent.Bookings));
        TestData.AssertBooking(booking, savedBooking);
        TestData.AssertEvent(parent, savedBooking.Event);
    }

    [Fact]
    public async Task MigrateAsync_RepeatedApplicationPreservesHistoryAndExistingLinkedData()
    {
        // Arrange
        await database.ResetAsync();
        Event parent = TestData.CreateEvent("Existing data");
        Assert.True(parent.TryReserveSeats());
        Booking booking = new Booking(parent.Id, Guid.NewGuid());
        booking.Confirm();
        await TestData.SeedAsync(database, [parent], booking);
        await using AppDbContext migrate = database.CreateContext();

        string[] expected = migrate.Database.GetMigrations().ToArray();
        string[] before = (await migrate.Database.GetAppliedMigrationsAsync(CancellationToken.None)).ToArray();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, before);

        // Act
        await migrate.Database.MigrateAsync(CancellationToken.None);

        // Assert
        await using AppDbContext verify = database.CreateContext();

        Assert.Equal(before, (await verify.Database.GetAppliedMigrationsAsync(CancellationToken.None)).ToArray());
        Assert.Empty(await verify.Database.GetPendingMigrationsAsync(CancellationToken.None));
        TestData.AssertEvent(parent, await verify.Events.AsNoTracking().SingleAsync(CancellationToken.None));
        TestData.AssertBooking(booking, await verify.Bookings.AsNoTracking().SingleAsync(CancellationToken.None));
    }
}
