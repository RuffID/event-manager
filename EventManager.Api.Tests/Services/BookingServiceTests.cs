using EventManager.Api.Exceptions;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Repositories;
using EventManager.Api.Services;
using Xunit;
using Event = EventManager.Api.Models.Event;

namespace EventManager.Api.Tests.Services
{
    public class BookingServiceTests
    {
        [Fact]
        public async Task CreateBookingAsync_ReturnsPendingBooking_WhenEventExists()
        {
            // Arrange
            Event existingEvent = CreateEvent();
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);

            // Act
            ServiceResult<BookingInfo> result = await service.CreateBookingAsync(existingEvent.Id);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.Error);
            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.NotEqual(Guid.Empty, booking.Id);
            Assert.Equal(existingEvent.Id, booking.EventId);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Null(booking.ProcessedAt);
            Assert.True(bookingRepository.Bookings.ContainsKey(booking.Id));
            Assert.Equal(9, existingEvent.AvailableSeats);
        }

        [Fact]
        public async Task CreateBookingAsync_CreatesUniqueBookings_WhenCalledUntilSeatLimit()
        {
            // Arrange
            const int totalSeats = 3;
            Event existingEvent = CreateEvent(totalSeats);
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);
            List<BookingInfo> bookings = new List<BookingInfo>();

            // Act
            foreach (int _ in Enumerable.Range(0, totalSeats))
            {
                ServiceResult<BookingInfo> result =
                    await service.CreateBookingAsync(existingEvent.Id);

                Assert.True(result.Success);
                bookings.Add(Assert.IsType<BookingInfo>(result.Data));
            }

            // Assert
            Assert.Equal(totalSeats, bookings.Count);
            Assert.Equal(totalSeats, bookings.Select(booking => booking.Id).Distinct().Count());
            Assert.Equal(totalSeats, bookingRepository.Bookings.Count);
            Assert.Equal(0, existingEvent.AvailableSeats);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsBooking_WhenBookingExists()
        {
            // Arrange
            Event existingEvent = CreateEvent();
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking storedBooking = new Booking(existingEvent.Id);
            AddBooking(bookingRepository, storedBooking);
            BookingService service = new BookingService(
                CreateEventRepository(existingEvent),
                bookingRepository);

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.Error);
            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(storedBooking.Id, booking.Id);
            Assert.Equal(storedBooking.EventId, booking.EventId);
            Assert.Equal(storedBooking.Status, booking.Status);
            Assert.Equal(storedBooking.CreatedAt, booking.CreatedAt);
            Assert.Equal(storedBooking.ProcessedAt, booking.ProcessedAt);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsConfirmedStatus_WhenBookingWasConfirmed()
        {
            // Arrange
            Event existingEvent = CreateEvent();
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking storedBooking = new Booking(existingEvent.Id);
            AddBooking(bookingRepository, storedBooking);
            BookingService service = new BookingService(
                CreateEventRepository(existingEvent),
                bookingRepository);
            storedBooking.Confirm();

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id);

            // Assert
            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsRejectedStatus_WhenBookingWasRejected()
        {
            // Arrange
            Event existingEvent = CreateEvent();
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking storedBooking = new Booking(existingEvent.Id);
            AddBooking(bookingRepository, storedBooking);
            BookingService service = new BookingService(
                CreateEventRepository(existingEvent),
                bookingRepository);
            storedBooking.Reject();

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id);

            // Assert
            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventDoesNotExist()
        {
            // Arrange
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(
                CreateEventRepository(),
                bookingRepository);

            // Act
            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(Guid.NewGuid());

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(bookingRepository.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventWasDeleted()
        {
            // Arrange
            Event deletedEvent = CreateEvent();
            InMemoryEventRepository eventRepository = CreateEventRepository(deletedEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);

            if (!eventRepository.Events.TryRemove(deletedEvent.Id, out _))
                throw new InvalidOperationException("The test event must exist before deletion.");

            // Act
            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(deletedEvent.Id);

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(bookingRepository.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventIdIsEmpty()
        {
            // Arrange
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(
                CreateEventRepository(),
                bookingRepository);

            // Act
            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(Guid.Empty);

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(bookingRepository.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ThrowsNoAvailableSeatsException_WhenEventIsFull()
        {
            // Arrange
            Event existingEvent = CreateEvent(totalSeats: 1);
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);
            await service.CreateBookingAsync(existingEvent.Id);

            // Act
            Func<Task> action = () => service.CreateBookingAsync(existingEvent.Id);

            // Assert
            NoAvailableSeatsException exception =
                await Assert.ThrowsAsync<NoAvailableSeatsException>(action);
            Assert.Equal("No available seats for this event", exception.Message);
            Assert.Single(bookingRepository.Bookings);
            Assert.Equal(0, existingEvent.AvailableSeats);
        }

        [Fact]
        public async Task CreateBookingAsync_PreventsOverbooking_WhenRequestsRunConcurrently()
        {
            // Arrange
            const int totalSeats = 5;
            const int requestCount = 20;
            Event existingEvent = CreateEvent(totalSeats);
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);

            Task<object>[] requests = Enumerable.Range(0, requestCount)
                .Select(_ => Task.Run(async () =>
                {
                    try
                    {
                        return (object)await service.CreateBookingAsync(existingEvent.Id);
                    }
                    catch (Exception exception)
                    {
                        return exception;
                    }
                }))
                .ToArray();

            // Act
            object[] outcomes = await Task.WhenAll(requests);

            // Assert
            ServiceResult<BookingInfo>[] successfulResults = outcomes
                .OfType<ServiceResult<BookingInfo>>()
                .Where(result => result.Success)
                .ToArray();
            NoAvailableSeatsException[] conflicts = outcomes
                .OfType<NoAvailableSeatsException>()
                .ToArray();
            Guid[] bookingIds = successfulResults
                .Select(result => Assert.IsType<BookingInfo>(result.Data).Id)
                .ToArray();

            Assert.Equal(totalSeats, successfulResults.Length);
            Assert.Equal(requestCount - totalSeats, conflicts.Length);
            Assert.Equal(totalSeats, bookingRepository.Bookings.Count);
            Assert.Equal(0, existingEvent.AvailableSeats);
            Assert.Equal(bookingIds.Length, bookingIds.Distinct().Count());
        }

        [Fact]
        public async Task CreateBookingAsync_CreatesUniqueBookings_WhenRequestsMatchSeatLimitConcurrently()
        {
            // Arrange
            const int totalSeats = 10;
            Event existingEvent = CreateEvent(totalSeats);
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);

            Task<ServiceResult<BookingInfo>>[] requests = Enumerable.Range(0, totalSeats)
                .Select(_ => Task.Run(() => service.CreateBookingAsync(existingEvent.Id)))
                .ToArray();

            // Act
            ServiceResult<BookingInfo>[] results = await Task.WhenAll(requests);

            // Assert
            Assert.All(results, result => Assert.True(result.Success));
            Guid[] bookingIds = results
                .Select(result => Assert.IsType<BookingInfo>(result.Data).Id)
                .ToArray();

            Assert.Equal(totalSeats, bookingIds.Distinct().Count());
            Assert.Equal(totalSeats, bookingRepository.Bookings.Count);
            Assert.Equal(0, existingEvent.AvailableSeats);
        }

        [Fact]
        public async Task CreateBookingAsync_DoesNotOverbook_WhenEventIsUpdatedConcurrently()
        {
            // Arrange
            const int totalSeats = 5;
            const int requestCount = 10;
            Event existingEvent = CreateEvent(totalSeats);
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService bookingService = new BookingService(
                eventRepository,
                bookingRepository);
            EventService eventService = new EventService(eventRepository);
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Обновлённое тестовое событие",
                StartAt = existingEvent.StartAt.AddHours(1),
                EndAt = existingEvent.EndAt.AddHours(1)
            };
            TaskCompletionSource<bool> start =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<ServiceResult<EventDto>> updateTask = Task.Run(async () =>
            {
                await start.Task;
                return eventService.UpdateEvent(existingEvent.Id, dto);
            });
            Task<object>[] bookingTasks = Enumerable.Range(0, requestCount)
                .Select(_ => Task.Run(async () =>
                {
                    await start.Task;

                    try
                    {
                        return (object)await bookingService.CreateBookingAsync(existingEvent.Id);
                    }
                    catch (NoAvailableSeatsException exception)
                    {
                        return exception;
                    }
                }))
                .ToArray();

            // Act
            start.SetResult(true);
            ServiceResult<EventDto> updateResult = await updateTask;
            object[] bookingOutcomes = await Task.WhenAll(bookingTasks);

            // Assert
            int successfulBookingCount = bookingOutcomes
                .OfType<ServiceResult<BookingInfo>>()
                .Count(result => result.Success);
            Event storedEvent = eventRepository.Events[existingEvent.Id];

            Assert.True(updateResult.Success);
            Assert.True(successfulBookingCount <= totalSeats);
            Assert.Equal(successfulBookingCount, bookingRepository.Bookings.Count);
            Assert.Equal(totalSeats - successfulBookingCount, storedEvent.AvailableSeats);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsNotFound_WhenBookingDoesNotExist()
        {
            // Arrange
            BookingService service = new BookingService(
                CreateEventRepository(),
                new InMemoryBookingRepository());

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(Guid.NewGuid());

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
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

        private static Event CreateEvent(int totalSeats = 10)
        {
            return new Event(
                Guid.NewGuid(),
                "Тестовое событие",
                null,
                new DateTime(2030, 1, 1, 10, 0, 0),
                new DateTime(2030, 1, 1, 12, 0, 0),
                totalSeats);
        }

        private static void AddBooking(
            InMemoryBookingRepository repository,
            Booking booking)
        {
            if (!repository.Bookings.TryAdd(booking.Id, booking))
                throw new InvalidOperationException("Booking identifiers in a test must be unique.");
        }
    }
}
