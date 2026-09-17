using EventManager.Api.BackgroundServices;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Repositories;
using EventManager.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManager.Api.Tests.BackgroundServices
{
    public class BookingProcessorTests
    {
        [Fact]
        public async Task ProcessPendingBookingsAsync_ConfirmsAndStoresBooking_WhenBookingIsPending()
        {
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            InMemoryEventRepository eventRepository = CreateEventRepository(@event);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking booking = new Booking(@event.Id);
            bookingRepository.Bookings.TryAdd(booking.Id, booking);
            BookingProcessor processor = CreateProcessor(
                bookingRepository,
                eventRepository,
                new ImmediateBookingProcessingDelay());

            await processor.ProcessPendingBookingsAsync(CancellationToken.None);

            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
            Assert.Same(booking, bookingRepository.Bookings[booking.Id]);
            Assert.Equal(0, @event.AvailableSeats);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_RejectsBookingAndAllowsAnotherBooking_WhenProcessingFails()
        {
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            InMemoryEventRepository eventRepository = CreateEventRepository(@event);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking booking = new Booking(@event.Id);
            bookingRepository.Bookings.TryAdd(booking.Id, booking);
            BookingProcessor processor = CreateProcessor(
                bookingRepository,
                eventRepository,
                new FailingBookingProcessingDelay());

            await processor.ProcessPendingBookingsAsync(CancellationToken.None);

            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
            Assert.Same(booking, bookingRepository.Bookings[booking.Id]);
            Assert.Equal(1, @event.AvailableSeats);

            BookingService bookingService = new BookingService(
                eventRepository,
                bookingRepository);
            ServiceResult<BookingInfo> result =
                await bookingService.CreateBookingAsync(@event.Id);

            Assert.True(result.Success);
            Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(0, @event.AvailableSeats);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
        {
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            InMemoryEventRepository eventRepository = CreateEventRepository(@event);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking booking = new Booking(@event.Id);
            bookingRepository.Bookings.TryAdd(booking.Id, booking);
            BookingProcessor processor = CreateProcessor(
                bookingRepository,
                eventRepository,
                new ImmediateBookingProcessingDelay());
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Task action = processor.ProcessPendingBookingsAsync(cancellationTokenSource.Token);

            await Assert.ThrowsAsync<OperationCanceledException>(() => action);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Null(booking.ProcessedAt);
            Assert.Equal(0, @event.AvailableSeats);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_RejectsBooking_WhenEventWasDeleted()
        {
            Event @event = CreateEvent();
            Assert.True(@event.TryReserveSeats());
            InMemoryEventRepository eventRepository = CreateEventRepository(@event);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking booking = new Booking(@event.Id);
            bookingRepository.Bookings.TryAdd(booking.Id, booking);
            Assert.True(eventRepository.Events.TryRemove(@event.Id, out _));
            BookingProcessor processor = CreateProcessor(
                bookingRepository,
                eventRepository,
                new ImmediateBookingProcessingDelay());

            await processor.ProcessPendingBookingsAsync(CancellationToken.None);

            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
            Assert.Same(booking, bookingRepository.Bookings[booking.Id]);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_StartsPendingBookingsInParallel()
        {
            const int bookingCount = 3;
            Event @event = CreateEvent(totalSeats: bookingCount);
            InMemoryEventRepository eventRepository = CreateEventRepository(@event);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();

            for (int index = 0; index < bookingCount; index++)
            {
                Assert.True(@event.TryReserveSeats());
                Booking booking = new Booking(@event.Id);
                bookingRepository.Bookings.TryAdd(booking.Id, booking);
            }

            CoordinatedBookingProcessingDelay processingDelay =
                new CoordinatedBookingProcessingDelay(bookingCount);
            BookingProcessor processor = CreateProcessor(
                bookingRepository,
                eventRepository,
                processingDelay);
            using CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(2));

            Task processingTask = processor.ProcessPendingBookingsAsync(
                cancellationTokenSource.Token);

            await processingDelay.AllCallsStarted.WaitAsync(cancellationTokenSource.Token);
            await processingTask;

            Assert.All(
                bookingRepository.Bookings.Values,
                booking => Assert.Equal(BookingStatus.Confirmed, booking.Status));
        }

        private static BookingProcessor CreateProcessor(
            InMemoryBookingRepository bookingRepository,
            InMemoryEventRepository eventRepository,
            IBookingProcessingDelay processingDelay)
        {
            return new BookingProcessor(
                bookingRepository,
                eventRepository,
                processingDelay,
                NullLogger<BookingProcessor>.Instance);
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

        private static InMemoryEventRepository CreateEventRepository(params Event[] events)
        {
            InMemoryEventRepository repository = new InMemoryEventRepository();
            repository.Events.Clear();

            foreach (Event @event in events)
            {
                if (!repository.Events.TryAdd(@event.Id, @event))
                    throw new InvalidOperationException("Event identifiers in a test must be unique.");
            }

            return repository;
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

        private class CoordinatedBookingProcessingDelay(int expectedCalls)
            : IBookingProcessingDelay
        {
            private readonly TaskCompletionSource<bool> _allCallsStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _callCount;

            public Task AllCallsStarted => _allCallsStarted.Task;

            public async Task WaitAsync(CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref _callCount) == expectedCalls)
                    _allCallsStarted.TrySetResult(true);

                await _allCallsStarted.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
