using EventManager.Api.Exceptions;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.DataAccess;
using EventManager.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Event = EventManager.Api.Models.Event;

namespace EventManager.Api.Tests.Services
{
    public class BookingServiceTests : IDisposable
    {
        private readonly ServiceTestContext _database = new();

        [Fact]
        public async Task CreateBookingAsync_ReturnsPendingBooking_WhenEventExists()
        {
            // Arrange
            Event existingEvent = CreateEvent();
            await _database.SeedAsync(existingEvent);
            IBookingService service = _database.BookingService;

            // Act
            ServiceResult<BookingInfo> result = await service.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.Error);
            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.NotEqual(Guid.Empty, booking.Id);
            Assert.Equal(existingEvent.Id, booking.EventId);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Null(booking.ProcessedAt);
            _database.Context.ChangeTracker.Clear();
            Assert.True(await _database.Context.Bookings.AnyAsync(entity => entity.Id == booking.Id, TestContext.Current.CancellationToken));
            Assert.Equal(9, (await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        [Fact]
        public async Task CreateBookingAsync_CreatesUniqueBookings_WhenCalledUntilSeatLimit()
        {
            // Arrange
            const int totalSeats = 3;
            Event existingEvent = CreateEvent(totalSeats);
            await _database.SeedAsync(existingEvent);
            IBookingService service = _database.BookingService;
            List<BookingInfo> bookings = new List<BookingInfo>();

            // Act
            foreach (int _ in Enumerable.Range(0, totalSeats))
            {
                ServiceResult<BookingInfo> result =
                    await service.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Success);
                bookings.Add(Assert.IsType<BookingInfo>(result.Data));
            }

            // Assert
            Assert.Equal(totalSeats, bookings.Count);
            Assert.Equal(totalSeats, bookings.Select(booking => booking.Id).Distinct().Count());
            _database.Context.ChangeTracker.Clear();
            Assert.Equal(totalSeats, await _database.Context.Bookings.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, (await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsBooking_WhenBookingExists()
        {
            // Arrange
            Event existingEvent = CreateEvent();
            Booking storedBooking = new Booking(existingEvent.Id);
            await SeedBookingAsync(existingEvent, storedBooking);
            IBookingService service = _database.BookingService;

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id, cancellationToken: TestContext.Current.CancellationToken);

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
            Booking storedBooking = new Booking(existingEvent.Id);
            storedBooking.Confirm();
            await SeedBookingAsync(existingEvent, storedBooking);
            IBookingService service = _database.BookingService;

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id, cancellationToken: TestContext.Current.CancellationToken);

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
            Booking storedBooking = new Booking(existingEvent.Id);
            storedBooking.Reject();
            await SeedBookingAsync(existingEvent, storedBooking);
            IBookingService service = _database.BookingService;

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(storedBooking.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            BookingInfo booking = Assert.IsType<BookingInfo>(result.Data);
            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventDoesNotExist()
        {
            // Arrange
            IBookingService service = _database.BookingService;

            // Act
            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(_database.Context.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventWasDeleted()
        {
            // Arrange
            Event deletedEvent = CreateEvent();
            await _database.SeedAsync(deletedEvent);
            IBookingService service = _database.BookingService;
            Event storedEvent = await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken);
            _database.Context.Events.Remove(storedEvent);
            await _database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(deletedEvent.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(_database.Context.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ReturnsNotFound_WhenEventIdIsEmpty()
        {
            // Arrange
            IBookingService service = _database.BookingService;

            // Act
            ServiceResult<BookingInfo> result =
                await service.CreateBookingAsync(Guid.Empty, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
            Assert.Empty(_database.Context.Bookings);
        }

        [Fact]
        public async Task CreateBookingAsync_ThrowsNoAvailableSeatsException_WhenEventIsFull()
        {
            // Arrange
            Event existingEvent = CreateEvent(totalSeats: 1);
            await _database.SeedAsync(existingEvent);
            IBookingService service = _database.BookingService;
            await service.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            Func<Task> action = () => service.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            NoAvailableSeatsException exception =
                await Assert.ThrowsAsync<NoAvailableSeatsException>(action);
            Assert.Equal("No available seats for this event", exception.Message);
            _database.Context.ChangeTracker.Clear();
            Assert.Single(_database.Context.Bookings);
            Assert.Equal(0, (await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        [Fact]
        public async Task CreateBookingAsync_PreventsOverbooking_WhenRequestsRunConcurrently()
        {
            // Arrange
            const int totalSeats = 5;
            const int requestCount = 20;
            Event existingEvent = CreateEvent(totalSeats);
            await _database.SeedAsync(existingEvent);

            // Act
            Task<object>[] requests = Enumerable.Range(0, requestCount)
                .Select(_ => Task.Run(async () =>
                {
                    using IServiceScope scope = _database.ServiceProvider.CreateScope();
                    IBookingService service = scope.ServiceProvider.GetRequiredService<IBookingService>();

                    try
                    {
                        return (object)await service.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);
                    }
                    catch (Exception exception)
                    {
                        return exception;
                    }
                }))
                .ToArray();

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
            Assert.Equal(totalSeats, await _database.Context.Bookings.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, (await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
            Assert.Equal(bookingIds.Length, bookingIds.Distinct().Count());
        }

        [Fact]
        public async Task CreateBookingAsync_CreatesUniqueBookings_WhenRequestsMatchSeatLimitConcurrently()
        {
            // Arrange
            const int totalSeats = 10;
            Event existingEvent = CreateEvent(totalSeats);
            await _database.SeedAsync(existingEvent);

            // Act
            Task<ServiceResult<BookingInfo>>[] requests = Enumerable.Range(0, totalSeats)
                .Select(_ => Task.Run(async () =>
                {
                    using IServiceScope scope = _database.ServiceProvider.CreateScope();
                    IBookingService service = scope.ServiceProvider.GetRequiredService<IBookingService>();
                    return await service.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);
                }))
                .ToArray();

            ServiceResult<BookingInfo>[] results = await Task.WhenAll(requests);

            // Assert
            Assert.All(results, result => Assert.True(result.Success));
            Guid[] bookingIds = results
                .Select(result => Assert.IsType<BookingInfo>(result.Data).Id)
                .ToArray();

            Assert.Equal(totalSeats, bookingIds.Distinct().Count());
            Assert.Equal(totalSeats, await _database.Context.Bookings.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, (await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        [Fact]
        public async Task CreateBookingAsync_DoesNotOverbook_WhenEventIsUpdatedConcurrently()
        {
            // Arrange
            const int totalSeats = 5;
            const int requestCount = 10;
            Event existingEvent = CreateEvent(totalSeats);
            await _database.SeedAsync(existingEvent);
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
                using IServiceScope scope = _database.ServiceProvider.CreateScope();
                IEventService eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
                return await eventService.UpdateEventAsync(existingEvent.Id, dto, cancellationToken: TestContext.Current.CancellationToken);
            });
            Task<object>[] bookingTasks = Enumerable.Range(0, requestCount)
                .Select(_ => Task.Run(async () =>
                {
                    await start.Task;
                    using IServiceScope scope = _database.ServiceProvider.CreateScope();
                    IBookingService bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                    try
                    {
                        return (object)await bookingService.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);
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
            Event storedEvent = await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken);

            Assert.True(updateResult.Success);
            Assert.Equal(totalSeats, successfulBookingCount);
            Assert.Equal(successfulBookingCount, await _database.Context.Bookings.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(totalSeats - successfulBookingCount, storedEvent.AvailableSeats);
            Assert.Equal(dto.Title, storedEvent.Title);
        }

        [Fact]
        public async Task GetBookingByIdAsync_ReturnsNotFound_WhenBookingDoesNotExist()
        {
            // Arrange
            IBookingService service = _database.BookingService;

            // Act
            ServiceResult<BookingInfo> result =
                await service.GetBookingByIdAsync(Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.Data);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
        }

        [Fact]
        public async Task CreateBookingAsync_UsesCurrentSeatCount_WhenAnotherScopeAlreadyLoadedEvent()
        {
            // Arrange
            Event existingEvent = CreateEvent(totalSeats: 1);
            await _database.SeedAsync(existingEvent);
            using IServiceScope otherScope = _database.ServiceProvider.CreateScope();
            AppDbContext otherContext = otherScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await otherContext.Events.SingleAsync(TestContext.Current.CancellationToken);
            IBookingService otherService = otherScope.ServiceProvider.GetRequiredService<IBookingService>();
            await _database.BookingService.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            Func<Task> action = () => otherService.CreateBookingAsync(existingEvent.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            await Assert.ThrowsAsync<NoAvailableSeatsException>(action);
            _database.Context.ChangeTracker.Clear();
            Assert.Single(_database.Context.Bookings);
            Assert.Equal(0, (await _database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        private async Task SeedBookingAsync(Event @event, Booking booking)
        {
            await _database.SeedAsync(@event);
            _database.Context.Bookings.Add(booking);
            await _database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _database.Context.ChangeTracker.Clear();
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

        public void Dispose() => _database.Dispose();
    }
}
