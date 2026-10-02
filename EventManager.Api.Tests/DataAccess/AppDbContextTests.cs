using EventManager.Api.DataAccess;
using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace EventManager.Api.Tests.DataAccess
{
    public class AppDbContextTests
    {
        [Fact]
        public async Task SaveChangesAsync_PreservesStateAndRelationships_AcrossContexts()
        {
            // Arrange
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            string databaseName = Guid.NewGuid().ToString();
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
            Event originalEvent = Event.Create(
                "Тестовое событие",
                "Описание события",
                new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                5);
            Assert.True(originalEvent.TryReserveSeats());
            Booking originalBooking = new Booking(originalEvent.Id);

            // Act
            await using (AppDbContext context = new AppDbContext(options))
            {
                context.Events.Add(originalEvent);
                context.Bookings.Add(originalBooking);
                await context.SaveChangesAsync(cancellationToken);
            }

            DateTime? processedAt;
            await using (AppDbContext context = new AppDbContext(options))
            {
                Event loadedEvent = await context.Events.Include(entity => entity.Bookings).SingleAsync(cancellationToken);

                // Assert
                Booking loadedBooking = Assert.Single(loadedEvent.Bookings);

                Assert.NotSame(originalEvent, loadedEvent);
                Assert.NotSame(originalBooking, loadedBooking);
                Assert.Equal(originalEvent.Id, loadedEvent.Id);
                Assert.Equal(originalEvent.Title, loadedEvent.Title);
                Assert.Equal(originalEvent.Description, loadedEvent.Description);
                Assert.Equal(originalEvent.StartAt, loadedEvent.StartAt);
                Assert.Equal(originalEvent.EndAt, loadedEvent.EndAt);
                Assert.Equal(5, loadedEvent.TotalSeats);
                Assert.Equal(4, loadedEvent.AvailableSeats);
                Assert.Equal(originalBooking.Id, loadedBooking.Id);
                Assert.Equal(originalEvent.Id, loadedBooking.EventId);
                Assert.Equal(originalBooking.CreatedAt, loadedBooking.CreatedAt);
                Assert.Equal(BookingStatus.Pending, loadedBooking.Status);
                Assert.Null(loadedBooking.ProcessedAt);
                Assert.Same(loadedEvent, loadedBooking.Event);

                // Arrange
                Assert.True(loadedEvent.TryReserveSeats());
                loadedBooking.Confirm();
                processedAt = loadedBooking.ProcessedAt;
                Assert.NotNull(processedAt);

                // Act
                await context.SaveChangesAsync(cancellationToken);
            }

            await using (AppDbContext context = new AppDbContext(options))
            {
                Booking loadedBooking = await context.Bookings.Include(entity => entity.Event).SingleAsync(cancellationToken);

                // Assert
                Assert.Equal(BookingStatus.Confirmed, loadedBooking.Status);
                Assert.Equal(processedAt, loadedBooking.ProcessedAt);
                Assert.Equal(3, loadedBooking.Event.AvailableSeats);
                Assert.Same(loadedBooking, Assert.Single(loadedBooking.Event.Bookings));
            }
        }

        [Fact]
        public void Model_BuildsPostgreSqlSchema_WithStringBookingStatus()
        {
            // Arrange
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=eventapi;Username=postgres;Password=postgres")
                .Options;
            using AppDbContext context = new AppDbContext(options);

            // Act
            IRelationalModel model = context.Model.GetRelationalModel();

            // Assert
            ITable eventsTable = Assert.IsAssignableFrom<ITable>(model.FindTable("events", null));
            ITable bookingsTable = Assert.IsAssignableFrom<ITable>(model.FindTable("bookings", null));
            IColumn statusColumn = Assert.IsAssignableFrom<IColumn>(bookingsTable.FindColumn(nameof(Booking.Status)));

            Assert.Equal(typeof(string), statusColumn.ProviderClrType);
            Assert.Equal(20, context.Model.FindEntityType(typeof(Booking))!.FindProperty(nameof(Booking.Status))!.GetMaxLength());
            Assert.Equal(200, context.Model.FindEntityType(typeof(Event))!.FindProperty(nameof(Event.Title))!.GetMaxLength());
            Assert.Equal(2000, context.Model.FindEntityType(typeof(Event))!.FindProperty(nameof(Event.Description))!.GetMaxLength());
            Assert.False(statusColumn.IsNullable);
            Assert.Same(eventsTable, Assert.Single(bookingsTable.ForeignKeyConstraints).PrincipalTable);
        }
    }
}
