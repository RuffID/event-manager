using EventManager.Api.BackgroundServices;
using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Api.Extensions;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Tests.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventManager.Api.Tests.DataAccess
{
    public class RepositoryContractTests
    {
        [Theory]
        [InlineData(typeof(AppDbContext))]
        [InlineData(typeof(IEventRepository))]
        [InlineData(typeof(IBookingRepository))]
        [InlineData(typeof(IUnitOfWork))]
        public void AddRepositories_SharesDependencyOnlyWithinScope(Type serviceType)
        {
            // Arrange
            ServiceCollection services = new ServiceCollection();
            string databaseName = Guid.NewGuid().ToString();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));

            // Act
            services.AddRepositories();
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
            using IServiceScope firstScope = provider.CreateScope();
            using IServiceScope secondScope = provider.CreateScope();

            object first = firstScope.ServiceProvider.GetRequiredService(serviceType);
            object repeated = firstScope.ServiceProvider.GetRequiredService(serviceType);
            object second = secondScope.ServiceProvider.GetRequiredService(serviceType);

            // Assert
            ServiceDescriptor descriptor = Assert.Single(services, item => item.ServiceType == serviceType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Same(first, repeated);
            Assert.NotSame(first, second);
        }

        [Fact]
        public async Task ReadQueries_LeaveChangeTrackerEmpty()
        {
            // Arrange
            using ServiceTestContext database = new ServiceTestContext();
            CancellationToken token = TestContext.Current.CancellationToken;
            Event @event = CreateEvent();
            Booking pending = new Booking(@event.Id);
            Booking confirmed = new Booking(@event.Id);
            confirmed.Confirm();
            await database.SeedAsync(@event);
            database.Context.Bookings.AddRange(pending, confirmed);
            await database.Context.SaveChangesAsync(token);
            database.Context.ChangeTracker.Clear();
            using IServiceScope scope = database.ServiceProvider.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            IEventRepository events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            IBookingRepository bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

            // Act
            Event? readEvent = await events.GetByIdAsync(@event.Id, token);
            Booking? readBooking = await bookings.GetByIdAsync(pending.Id, token);
            EventPage page = await events.GetPageAsync(null, null, null, 1, 10, token);
            IReadOnlyList<Guid> pendingIds = await bookings.GetPendingIdsAsync(token);
            bool exists = await events.ExistsAsync(@event.Id, token);

            // Assert
            Assert.NotNull(readEvent);
            Assert.NotNull(readBooking);
            Assert.Equal(@event.Id, Assert.Single(page.Items).Id);
            Assert.Equal(pending.Id, Assert.Single(pendingIds));
            Assert.True(exists);
            Assert.Empty(context.ChangeTracker.Entries());
        }

        [Fact]
        public async Task UnitOfWork_SavesAddedEntitiesFromBothRepositories_OnlyAtExplicitCommit()
        {
            // Arrange
            using ServiceTestContext database = new ServiceTestContext();
            CancellationToken token = TestContext.Current.CancellationToken;
            using IServiceScope scope = database.ServiceProvider.CreateScope();
            IEventRepository events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            IBookingRepository bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            Event @event = CreateEvent();
            Booking booking = new Booking(@event.Id);

            // Act
            events.Add(@event);
            bookings.Add(booking);

            // Assert
            Assert.False(await database.Context.Events.AnyAsync(token));
            Assert.False(await database.Context.Bookings.AnyAsync(token));

            // Act
            int savedEntries = await unitOfWork.SaveChangesAsync(token);

            // Assert
            Assert.Equal(2, savedEntries);
            Assert.Equal(@event.Id, (await database.Context.Events.SingleAsync(token)).Id);
            Assert.Equal(booking.Id, (await database.Context.Bookings.SingleAsync(token)).Id);
        }

        [Fact]
        public async Task UnitOfWork_SavesTrackedChangesFromBothRepositoriesTogether()
        {
            // Arrange
            using ServiceTestContext database = new ServiceTestContext();
            CancellationToken token = TestContext.Current.CancellationToken;
            Event @event = CreateEvent();
            Booking booking = new Booking(@event.Id);
            await database.SeedAsync(@event);
            database.Context.Bookings.Add(booking);
            await database.Context.SaveChangesAsync(token);
            database.Context.ChangeTracker.Clear();
            using IServiceScope scope = database.ServiceProvider.CreateScope();
            IEventRepository events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            IBookingRepository bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            Event trackedEvent = Assert.IsType<Event>(await events.GetByIdForUpdateAsync(@event.Id, token));
            Booking trackedBooking = Assert.IsType<Booking>(await bookings.GetByIdForUpdateAsync(booking.Id, token));

            // Act
            Assert.True(trackedEvent.TryReserveSeats());
            trackedBooking.Confirm();
            int savedEntries = await unitOfWork.SaveChangesAsync(token);

            // Assert
            Assert.Equal(2, savedEntries);
            Assert.Equal(1, (await database.Context.Events.SingleAsync(token)).AvailableSeats);
            Assert.Equal(BookingStatus.Confirmed, (await database.Context.Bookings.SingleAsync(token)).Status);
        }

        [Fact]
        public async Task CreateBooking_SavesSeatAndBookingInOneCall()
        {
            // Arrange
            SaveBoundaryInterceptor interceptor = new SaveBoundaryInterceptor();
            using ServiceTestContext database = new ServiceTestContext(interceptor: interceptor);
            Event @event = CreateEvent();
            await database.SeedAsync(@event);
            interceptor.Reset();
            CancellationToken token = TestContext.Current.CancellationToken;

            // Act
            ServiceResult<BookingInfo> result = await database.BookingService.CreateBookingAsync(@event.Id, token);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, interceptor.CallCount);
            Assert.Equal(EntityState.Modified, interceptor.EventState);
            Assert.Equal(EntityState.Added, interceptor.BookingState);
            Assert.Equal(token, interceptor.ReceivedToken);
        }

        [Fact]
        public async Task ProcessingFailure_SavesRejectionAndReleasedSeatInOneCall()
        {
            // Arrange
            SaveBoundaryInterceptor interceptor = new SaveBoundaryInterceptor();
            using ServiceTestContext database = new ServiceTestContext(new FailingProcessingDelay(), interceptor);
            Event @event = CreateEvent();
            await database.SeedAsync(@event);
            await database.BookingService.CreateBookingAsync(@event.Id, TestContext.Current.CancellationToken);
            interceptor.Reset();

            // Act
            await database.BookingProcessor.ProcessPendingBookingsAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, interceptor.CallCount);
            Assert.Equal(EntityState.Modified, interceptor.EventState);
            Assert.Equal(EntityState.Modified, interceptor.BookingState);
            Assert.Equal(CancellationToken.None, interceptor.ReceivedToken);
            database.Context.ChangeTracker.Clear();
            Assert.Equal(2, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
            Assert.Equal(BookingStatus.Rejected,
                (await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken)).Status);
        }

        private static Event CreateEvent()
        {
            DateTime startAt = DateTime.UtcNow.AddDays(1);

            return Event.Create("Тестовое событие", null, startAt, startAt.AddHours(2), 2);
        }

        private class FailingProcessingDelay : IBookingProcessingDelay
        {
            public Task WaitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                return Task.FromException(new InvalidOperationException("Processing failed."));
            }
        }

        private class SaveBoundaryInterceptor : SaveChangesInterceptor
        {
            public int CallCount { get; private set; }

            public EntityState? EventState { get; private set; }

            public EntityState? BookingState { get; private set; }

            public CancellationToken ReceivedToken { get; private set; }

            public void Reset()
            {
                CallCount = 0;
                EventState = null;
                BookingState = null;
                ReceivedToken = CancellationToken.None;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                AppDbContext context = (AppDbContext)eventData.Context!;
                CallCount++;
                EventState = context.ChangeTracker.Entries<Event>()
                    .Where(entry => entry.State != EntityState.Unchanged)
                    .Select(entry => (EntityState?)entry.State)
                    .SingleOrDefault();
                BookingState = context.ChangeTracker.Entries<Booking>()
                    .Where(entry => entry.State != EntityState.Unchanged)
                    .Select(entry => (EntityState?)entry.State)
                    .SingleOrDefault();
                ReceivedToken = cancellationToken;

                return ValueTask.FromResult(result);
            }
        }
    }
}
