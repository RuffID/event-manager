using EventManager.Api.BackgroundServices;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Services;
using EventManager.Api.Tests.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventManager.Api.Tests.BackgroundServices
{
    public class BookingProcessorTests
    {
        [Fact]
        public async Task ProcessPendingBookingsAsync_ConfirmsAndStoresBooking_WhenBookingIsPending()
        {
            // Arrange
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            Booking booking = new Booking(@event.Id);
            using ServiceTestContext database = new ServiceTestContext(new ImmediateBookingProcessingDelay());
            await SeedAsync(database, @event, booking);
            BookingProcessor processor = database.BookingProcessor;

            // Act
            await processor.ProcessPendingBookingsAsync(TestContext.Current.CancellationToken);

            // Assert
            Booking storedBooking = await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(BookingStatus.Confirmed, storedBooking.Status);
            Assert.NotNull(storedBooking.ProcessedAt);
            Assert.Equal(booking.Id, storedBooking.Id);
            Assert.Equal(0, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_RejectsBookingAndAllowsAnotherBooking_WhenProcessingFails()
        {
            // Arrange
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            Booking booking = new Booking(@event.Id);
            using ServiceTestContext database = new ServiceTestContext(new FailingBookingProcessingDelay());
            await SeedAsync(database, @event, booking);
            BookingProcessor processor = database.BookingProcessor;

            // Act
            await processor.ProcessPendingBookingsAsync(TestContext.Current.CancellationToken);

            // Assert
            Booking storedBooking = await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(BookingStatus.Rejected, storedBooking.Status);
            Assert.NotNull(storedBooking.ProcessedAt);
            Assert.Equal(1, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);

            // Arrange
            IBookingService bookingService = database.BookingService;

            // Act
            ServiceResult<BookingInfo> result =
                await bookingService.CreateBookingAsync(@event.Id);

            // Assert
            Assert.True(result.Success);
            Assert.IsType<BookingInfo>(result.Data);
            database.Context.ChangeTracker.Clear();
            Assert.Equal(0, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
        {
            // Arrange
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            Booking booking = new Booking(@event.Id);
            using ServiceTestContext database = new ServiceTestContext(new ImmediateBookingProcessingDelay());
            await SeedAsync(database, @event, booking);
            BookingProcessor processor = database.BookingProcessor;
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act
            Task action = processor.ProcessPendingBookingsAsync(cancellationTokenSource.Token);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
            Booking storedBooking = await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(BookingStatus.Pending, storedBooking.Status);
            Assert.Null(storedBooking.ProcessedAt);
            Assert.Equal(0, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_RejectsBooking_WhenEventIsMissing()
        {
            // Arrange
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            Booking booking = new Booking(@event.Id);
            using ServiceTestContext database = new ServiceTestContext(new ImmediateBookingProcessingDelay());
            // InMemory допускает отсутствие события; PostgreSQL защищает связь внешним ключом.
            database.Context.Bookings.Add(booking);
            await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
            database.Context.ChangeTracker.Clear();
            BookingProcessor processor = database.BookingProcessor;

            // Act
            await processor.ProcessPendingBookingsAsync(TestContext.Current.CancellationToken);

            // Assert
            Booking storedBooking = await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(BookingStatus.Rejected, storedBooking.Status);
            Assert.NotNull(storedBooking.ProcessedAt);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_StartsPendingBookingsInParallel()
        {
            // Arrange
            const int bookingCount = 3;
            Event @event = CreateEvent(totalSeats: bookingCount);
            List<Booking> bookings = new List<Booking>();

            for (int index = 0; index < bookingCount; index++)
            {
                Assert.True(@event.TryReserveSeats());
                Booking booking = new Booking(@event.Id);
                bookings.Add(booking);
            }

            CoordinatedBookingProcessingDelay processingDelay =
                new CoordinatedBookingProcessingDelay(bookingCount);
            using ServiceTestContext database = new ServiceTestContext(processingDelay);
            await SeedAsync(database, @event, bookings.ToArray());
            BookingProcessor processor = database.BookingProcessor;
            using CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            Task processingTask = processor.ProcessPendingBookingsAsync(
                cancellationTokenSource.Token);

            await processingDelay.AllCallsStarted.WaitAsync(cancellationTokenSource.Token);
            await processingTask;

            // Assert
            Assert.All(
                await database.Context.Bookings.ToListAsync(TestContext.Current.CancellationToken),
                booking => Assert.Equal(BookingStatus.Confirmed, booking.Status));
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_ReturnsAllSeatsOnce_WhenParallelProcessingFails()
        {
            // Arrange
            const int bookingCount = 3;
            Event @event = CreateEvent(totalSeats: bookingCount);
            Assert.True(@event.TryReserveSeats(bookingCount));
            Booking[] bookings = Enumerable.Range(0, bookingCount)
                .Select(_ => new Booking(@event.Id)).ToArray();
            CoordinatedBookingProcessingDelay processingDelay = new CoordinatedBookingProcessingDelay(bookingCount, true);
            using ServiceTestContext database = new ServiceTestContext(processingDelay);
            await SeedAsync(database, @event, bookings);
            using CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            await database.BookingProcessor.ProcessPendingBookingsAsync(cancellationTokenSource.Token);
            await database.BookingProcessor.ProcessPendingBookingsAsync(cancellationTokenSource.Token);

            // Assert
            Assert.Equal(bookingCount, processingDelay.CallCount);
            Assert.Equal(bookingCount, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
            List<Booking> storedBookings = await database.Context.Bookings.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(bookingCount, storedBookings.Count);
            Assert.All(storedBookings, booking =>
            {
                Assert.Equal(BookingStatus.Rejected, booking.Status);
                Assert.NotNull(booking.ProcessedAt);
            });
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_ProcessesEachBookingOnce_WhenRunsOverlap()
        {
            // Arrange
            const int bookingCount = 3;
            Event @event = CreateEvent(totalSeats: bookingCount);
            Assert.True(@event.TryReserveSeats(bookingCount));
            Booking[] bookings = Enumerable.Range(0, bookingCount)
                .Select(_ => new Booking(@event.Id)).ToArray();
            CoordinatedBookingProcessingDelay processingDelay = new CoordinatedBookingProcessingDelay(bookingCount);
            using ServiceTestContext database = new ServiceTestContext(processingDelay);
            await SeedAsync(database, @event, bookings);
            using CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            Task firstRun = database.BookingProcessor.ProcessPendingBookingsAsync(cancellationTokenSource.Token);
            Task secondRun = database.BookingProcessor.ProcessPendingBookingsAsync(cancellationTokenSource.Token);
            await Task.WhenAll(firstRun, secondRun);

            // Assert
            Assert.Equal(bookingCount, processingDelay.CallCount);
            Assert.All(await database.Context.Bookings.ToListAsync(TestContext.Current.CancellationToken),
                booking => Assert.Equal(BookingStatus.Confirmed, booking.Status));
            Assert.Equal(0, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        private static async Task SeedAsync(ServiceTestContext database, Event @event, params Booking[] bookings)
        {
            await database.SeedAsync(@event);
            database.Context.Bookings.AddRange(bookings);
            await database.Context.SaveChangesAsync();
            database.Context.ChangeTracker.Clear();
        }

        private static Event CreateEvent(int totalSeats = 1)
        {
            return Event.Create(
                "Тестовое событие",
                null,
                new DateTime(2030, 1, 1, 10, 0, 0),
                new DateTime(2030, 1, 1, 12, 0, 0),
                totalSeats);
        }

        private class ImmediateBookingProcessingDelay : IBookingProcessingDelay
        {
            public Task WaitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private class FailingBookingProcessingDelay : IBookingProcessingDelay
        {
            public Task WaitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromException(new InvalidOperationException("Processing failed."));
            }
        }

        private class CoordinatedBookingProcessingDelay(int expectedCalls, bool fail = false)
            : IBookingProcessingDelay
        {
            private readonly TaskCompletionSource<bool> _allCallsStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _callCount;

            public Task AllCallsStarted => _allCallsStarted.Task;

            public int CallCount => Volatile.Read(ref _callCount);

            public async Task WaitAsync(CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref _callCount) == expectedCalls)
                    _allCallsStarted.TrySetResult(true);

                await _allCallsStarted.Task.WaitAsync(cancellationToken);

                if (fail)
                    throw new InvalidOperationException("Processing failed.");
            }
        }
    }
}
