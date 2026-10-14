using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Api.IntegrationTests.Infrastructure;
using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventManager.Api.IntegrationTests.Migrations;

[Collection(PostgreSqlCollection.NAME)]
public class MigrationTests(PostgresFixture database)
{
    [Fact]
    public async Task MigrateAsync_CreatesUsableSchemaFromEmptyDatabaseAndAppliesEntireAssemblyChain()
    {
        // Arrange
        await database.DeleteDatabaseAsync();
        await using AppDbContext migrate = database.CreateContext();

        string[] expected = migrate.Database.GetMigrations().ToArray();
        Assert.NotEmpty(expected);
        Event parent = TestData.CreateEvent("Migration schema");
        Booking booking = new Booking(parent.Id);

        // Act
        await migrate.Database.MigrateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expected, (await migrate.Database.GetAppliedMigrationsAsync(CancellationToken.None)).ToArray());
        Assert.Empty(await migrate.Database.GetPendingMigrationsAsync(CancellationToken.None));
        await using (AppDbContext write = database.CreateContext())
        {
            new EventRepository(write).Add(parent);
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
        Booking booking = new Booking(parent.Id);
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
