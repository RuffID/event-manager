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
            Event existingEvent = CreateEvent();
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);

            ServiceResult<BookingInfo> result = await service.CreateBookingAsync(existingEvent.Id);

            Assert.True(result.Success);
            Assert.Null(result.Error);
            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.NotEqual(Guid.Empty, booking.Id);
            Assert.Equal(existingEvent.Id, booking.EventId);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Null(booking.ProcessedAt);
            Assert.True(bookingRepository.Bookings.ContainsKey(booking.Id));
        }

        [Fact]
        public async Task CreateBookingAsync_CreatesUniqueBookings_WhenCalledForSameEvent()
        {
            Event existingEvent = CreateEvent();
            InMemoryEventRepository eventRepository = CreateEventRepository(existingEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);

            ServiceResult<BookingInfo> firstResult =
                await service.CreateBookingAsync(existingEvent.Id);
            ServiceResult<BookingInfo> secondResult =
                await service.CreateBookingAsync(existingEvent.Id);

            BookingInfo firstBooking = Assert.IsType<BookingInfo>(firstResult.Data);
            BookingInfo secondBooking = Assert.IsType<BookingInfo>(secondResult.Data);
            Assert.NotEqual(firstBooking.Id, secondBooking.Id);
            Assert.Equal(2, bookingRepository.Bookings.Count);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsBooking_WhenBookingExists()
        {
            Event existingEvent = CreateEvent();
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking storedBooking = new Booking(existingEvent.Id);
            AddBooking(bookingRepository, storedBooking);
            BookingService service = new BookingService(
                CreateEventRepository(existingEvent),
                bookingRepository);

            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id);

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
            Event existingEvent = CreateEvent();
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking storedBooking = new Booking(existingEvent.Id);
            AddBooking(bookingRepository, storedBooking);
            BookingService service = new BookingService(
                CreateEventRepository(existingEvent),
                bookingRepository);
            storedBooking.Confirm();

            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id);

            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsRejectedStatus_WhenBookingWasRejected()
        {
            Event existingEvent = CreateEvent();
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            Booking storedBooking = new Booking(existingEvent.Id);
            AddBooking(bookingRepository, storedBooking);
            BookingService service = new BookingService(
                CreateEventRepository(existingEvent),
                bookingRepository);
            storedBooking.Reject();

            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id);

            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventDoesNotExist()
        {
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(
                CreateEventRepository(),
                bookingRepository);

            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(Guid.NewGuid());

            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(bookingRepository.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventWasDeleted()
        {
            Event deletedEvent = CreateEvent();
            InMemoryEventRepository eventRepository = CreateEventRepository(deletedEvent);
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(eventRepository, bookingRepository);

            if (!eventRepository.Events.TryRemove(deletedEvent.Id, out _))
                throw new InvalidOperationException("The test event must exist before deletion.");

            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(deletedEvent.Id);

            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(bookingRepository.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventIdIsEmpty()
        {
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            BookingService service = new BookingService(
                CreateEventRepository(),
                bookingRepository);

            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(Guid.Empty);

            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(bookingRepository.Bookings);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsNotFound_WhenBookingDoesNotExist()
        {
            BookingService service = new BookingService(
                CreateEventRepository(),
                new InMemoryBookingRepository());

            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(Guid.NewGuid());

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

        private static Event CreateEvent()
        {
            return new Event(
                Guid.NewGuid(),
                "Тестовое событие",
                null,
                new DateTime(2030, 1, 1, 10, 0, 0),
                new DateTime(2030, 1, 1, 12, 0, 0));
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
