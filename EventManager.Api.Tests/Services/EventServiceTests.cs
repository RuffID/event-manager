using EventManager.Api.Models.Dtos;
using EventManager.Api.Models;
using EventManager.Api.Models.Results;
using EventManager.Api.DataAccess;
using Microsoft.EntityFrameworkCore;
using EventManager.Api.Services;
using Xunit;
using Event = EventManager.Api.Models.Event;

namespace EventManager.Api.Tests.Services
{
    public class EventServiceTests : IDisposable
    {
        private readonly ServiceTestContext _database = new();

        [Fact]
        public async Task CreateEvent_ReturnsCreatedEvent_WhenDataIsValid()
        {
            // Arrange
            AppDbContext context = await CreateContextAsync();
            IEventService service = _database.EventService;
            CreateEventDto dto = new CreateEventDto
            {
                Title = "Новая встреча",
                Description = "Описание встречи",
                StartAt = new DateTime(2026, 11, 10, 18, 0, 0),
                EndAt = new DateTime(2026, 11, 10, 20, 0, 0),
                TotalSeats = 25
            };

            // Act
            ServiceResult<EventDto> result = await service.CreateEventAsync(dto);

            // Assert
            Assert.True(result.Success);
            EventDto createdEvent = Assert.IsType<EventDto>(result.Data);
            Assert.Equal(dto.Title, createdEvent.Title);
            Assert.Equal(dto.TotalSeats, createdEvent.TotalSeats);
            Assert.Equal(dto.TotalSeats, createdEvent.AvailableSeats);
            context.ChangeTracker.Clear();
            Assert.True(await context.Events.AnyAsync(entity => entity.Id == createdEvent.Id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetEvents_ReturnsAllEvents_WhenFiltersAreNotSpecified()
        {
            // Arrange
            Event firstEvent = CreateStoredEvent(
                "Первая встреча",
                new DateTime(2026, 11, 1, 10, 0, 0),
                new DateTime(2026, 11, 1, 12, 0, 0));

            Event secondEvent = CreateStoredEvent(
                "Вторая встреча",
                new DateTime(2026, 11, 2, 10, 0, 0),
                new DateTime(2026, 11, 2, 12, 0, 0));

            await CreateContextAsync(firstEvent, secondEvent);
            IEventService service = _database.EventService;

            // Act
            PaginatedResult result = await service.GetEventsAsync();

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Events.Count);
            Assert.Contains(result.Events, item => item.Id == firstEvent.Id);
            Assert.Contains(result.Events, item => item.Id == secondEvent.Id);
        }

        [Fact]
        public async Task GetEventById_ReturnsEvent_WhenEventExists()
        {
            // Arrange
            Event existingEvent = CreateStoredEvent(
                "Существующая встреча",
                new DateTime(2026, 11, 3, 10, 0, 0),
                new DateTime(2026, 11, 3, 12, 0, 0));

            await CreateContextAsync(existingEvent);
            IEventService service = _database.EventService;

            // Act
            ServiceResult<EventDto> result = await service.GetEventByIdAsync(existingEvent.Id);

            // Assert
            Assert.True(result.Success);
            EventDto foundEvent = Assert.IsType<EventDto>(result.Data);
            Assert.Equal(existingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public async Task UpdateEvent_ReturnsUpdatedEvent_WhenEventExists()
        {
            // Arrange
            Event existingEvent = CreateStoredEvent(
                "Старое название",
                new DateTime(2026, 11, 4, 10, 0, 0),
                new DateTime(2026, 11, 4, 12, 0, 0));

            AppDbContext context = await CreateContextAsync(existingEvent);
            IEventService service = _database.EventService;
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Новое название",
                Description = "Новое описание",
                StartAt = new DateTime(2026, 11, 4, 13, 0, 0),
                EndAt = new DateTime(2026, 11, 4, 15, 0, 0)
            };

            // Act
            ServiceResult<EventDto> result = await service.UpdateEventAsync(existingEvent.Id, dto);

            // Assert
            Assert.True(result.Success);
            EventDto updatedEvent = Assert.IsType<EventDto>(result.Data);
            Assert.Equal(dto.Title, updatedEvent.Title);
            Assert.Equal(dto.StartAt, updatedEvent.StartAt);
            context.ChangeTracker.Clear();
            Event storedEvent = await context.Events.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(dto.Title, storedEvent.Title);
        }

        [Fact]
        public async Task DeleteEvent_RemovesEvent_WhenEventExists()
        {
            // Arrange
            Event existingEvent = CreateStoredEvent(
                "Встреча для удаления",
                new DateTime(2026, 11, 5, 10, 0, 0),
                new DateTime(2026, 11, 5, 12, 0, 0));

            AppDbContext context = await CreateContextAsync(existingEvent);
            IEventService service = _database.EventService;

            // Act
            ServiceResult result = await service.DeleteEventAsync(existingEvent.Id);

            // Assert
            Assert.True(result.Success);
            context.ChangeTracker.Clear();
            Assert.False(await context.Events.AnyAsync(entity => entity.Id == existingEvent.Id, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetEvents_ReturnsMatchingEvents_WhenTitleFilterIsSpecified()
        {
            // Arrange
            Event matchingEvent = CreateStoredEvent(
                "C# Meetup",
                new DateTime(2026, 11, 6, 10, 0, 0),
                new DateTime(2026, 11, 6, 12, 0, 0));

            Event otherEvent = CreateStoredEvent(
                "Java Meetup",
                new DateTime(2026, 11, 7, 10, 0, 0),
                new DateTime(2026, 11, 7, 12, 0, 0));

            await CreateContextAsync(matchingEvent, otherEvent);
            IEventService service = _database.EventService;

            // Act
            PaginatedResult result = await service.GetEventsAsync(title: "c# meet");

            // Assert
            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(matchingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public async Task GetEvents_ReturnsEventsInsideRange_WhenDateFiltersAreSpecified()
        {
            // Arrange
            Event matchingEvent = CreateStoredEvent(
                "Встреча в диапазоне",
                new DateTime(2026, 12, 10, 10, 0, 0),
                new DateTime(2026, 12, 10, 12, 0, 0));

            Event earlyEvent = CreateStoredEvent(
                "Ранняя встреча",
                new DateTime(2026, 12, 1, 10, 0, 0),
                new DateTime(2026, 12, 1, 12, 0, 0));

            Event lateEvent = CreateStoredEvent(
                "Поздняя встреча",
                new DateTime(2026, 12, 20, 10, 0, 0),
                new DateTime(2027, 1, 2, 12, 0, 0));

            await CreateContextAsync(matchingEvent, earlyEvent, lateEvent);
            IEventService service = _database.EventService;

            // Act
            PaginatedResult result = await service.GetEventsAsync(
                from: new DateTime(2026, 12, 5),
                to: new DateTime(2026, 12, 31, 23, 59, 59));

            // Assert
            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(matchingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public async Task GetEvents_ReturnsRequestedPage_WhenPaginationIsSpecified()
        {
            // Arrange
            Event firstEvent = CreateStoredEvent(
                "Первая встреча",
                new DateTime(2027, 1, 1, 10, 0, 0),
                new DateTime(2027, 1, 1, 12, 0, 0));

            Event secondEvent = CreateStoredEvent(
                "Вторая встреча",
                new DateTime(2027, 1, 2, 10, 0, 0),
                new DateTime(2027, 1, 2, 12, 0, 0));

            Event thirdEvent = CreateStoredEvent(
                "Третья встреча",
                new DateTime(2027, 1, 3, 10, 0, 0),
                new DateTime(2027, 1, 3, 12, 0, 0));

            Event fourthEvent = CreateStoredEvent(
                "Четвёртая встреча",
                new DateTime(2027, 1, 4, 10, 0, 0),
                new DateTime(2027, 1, 4, 12, 0, 0));

            Event fifthEvent = CreateStoredEvent(
                "Пятая встреча",
                new DateTime(2027, 1, 5, 10, 0, 0),
                new DateTime(2027, 1, 5, 12, 0, 0));

            await CreateContextAsync(
                firstEvent,
                secondEvent,
                thirdEvent,
                fourthEvent,
                fifthEvent);
            IEventService service = _database.EventService;

            // Act
            PaginatedResult result = await service.GetEventsAsync(page: 2, pageSize: 2);

            // Assert
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(2, result.Page);
            Assert.Equal(2, result.PageSize);
            Guid[] expectedIds = [thirdEvent.Id, fourthEvent.Id];
            Assert.Equal(expectedIds, result.Events.Select(item => item.Id));
        }

        [Theory]
        [InlineData(0, 10, "page")]
        [InlineData(-1, 10, "page")]
        [InlineData(1, 0, "pageSize")]
        [InlineData(1, -1, "pageSize")]
        public async Task GetEvents_ThrowsArgumentOutOfRangeException_WhenPaginationIsInvalid(
            int page,
            int pageSize,
            string parameterName)
        {
            // Arrange
            IEventService service = _database.EventService;

            // Act
            Func<Task> action = () => service.GetEventsAsync(page: page, pageSize: pageSize);

            // Assert
            ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(action);
            Assert.Equal(parameterName, exception.ParamName);
        }

        [Fact]
        public async Task GetEvents_ReturnsMatchingEvents_WhenFiltersAreCombined()
        {
            // Arrange
            Event matchingEvent = CreateStoredEvent(
                "C# Workshop",
                new DateTime(2027, 2, 10, 10, 0, 0),
                new DateTime(2027, 2, 10, 12, 0, 0));

            Event otherTitleEvent = CreateStoredEvent(
                "Java Workshop",
                new DateTime(2027, 2, 10, 10, 0, 0),
                new DateTime(2027, 2, 10, 12, 0, 0));

            Event earlyEvent = CreateStoredEvent(
                "C# Early Workshop",
                new DateTime(2027, 2, 1, 10, 0, 0),
                new DateTime(2027, 2, 1, 12, 0, 0));

            Event lateEvent = CreateStoredEvent(
                "C# Late Workshop",
                new DateTime(2027, 2, 20, 10, 0, 0),
                new DateTime(2027, 3, 2, 12, 0, 0));

            await CreateContextAsync(
                matchingEvent,
                otherTitleEvent,
                earlyEvent,
                lateEvent);
            IEventService service = _database.EventService;

            // Act
            PaginatedResult result = await service.GetEventsAsync(
                title: "c#",
                from: new DateTime(2027, 2, 5),
                to: new DateTime(2027, 2, 28, 23, 59, 59));

            // Assert
            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(matchingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public async Task GetEvents_ReturnsAllEvents_WhenTitleFilterContainsOnlyWhitespace()
        {
            // Arrange
            Event firstEvent = CreateStoredEvent(
                "Первая встреча",
                new DateTime(2027, 2, 1, 10, 0, 0),
                new DateTime(2027, 2, 1, 12, 0, 0));

            Event secondEvent = CreateStoredEvent(
                "Вторая встреча",
                new DateTime(2027, 2, 2, 10, 0, 0),
                new DateTime(2027, 2, 2, 12, 0, 0));

            await CreateContextAsync(firstEvent, secondEvent);
            IEventService service = _database.EventService;

            // Act
            PaginatedResult result = await service.GetEventsAsync(title: "   ");

            // Assert
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Events.Count);
        }

        [Fact]
        public async Task GetEvents_IncludesEventsOnDateFilterBoundaries()
        {
            // Arrange
            DateTime from = new DateTime(2027, 2, 10, 10, 0, 0);
            DateTime to = new DateTime(2027, 2, 10, 12, 0, 0);
            Event boundaryEvent = CreateStoredEvent("Граничная встреча", from, to);
            await CreateContextAsync(boundaryEvent);
            IEventService service = _database.EventService;

            // Act
            PaginatedResult result = await service.GetEventsAsync(from: from, to: to);

            // Assert
            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(boundaryEvent.Id, foundEvent.Id);
        }

        [Fact]
        public async Task GetEventById_ReturnsNotFoundError_WhenEventDoesNotExist()
        {
            // Arrange
            IEventService service = _database.EventService;

            // Act
            ServiceResult<EventDto> result = await service.GetEventByIdAsync(Guid.NewGuid());

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
        }

        [Fact]
        public async Task UpdateEvent_ReturnsNotFoundError_WhenEventDoesNotExist()
        {
            // Arrange
            IEventService service = _database.EventService;
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Обновлённая встреча",
                StartAt = new DateTime(2027, 3, 1, 10, 0, 0),
                EndAt = new DateTime(2027, 3, 1, 12, 0, 0)
            };

            // Act
            ServiceResult<EventDto> result = await service.UpdateEventAsync(Guid.NewGuid(), dto);

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
        }

        [Fact]
        public async Task DeleteEvent_ReturnsNotFoundError_WhenEventDoesNotExist()
        {
            // Arrange
            IEventService service = _database.EventService;

            // Act
            ServiceResult result = await service.DeleteEventAsync(Guid.NewGuid());

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
        }

        [Fact]
        public async Task CreateEvent_ReturnsValidationError_WhenDataIsInvalid()
        {
            // Arrange
            IEventService service = _database.EventService;
            CreateEventDto dto = new CreateEventDto
            {
                Title = " ",
                StartAt = new DateTime(2027, 3, 2, 10, 0, 0),
                EndAt = new DateTime(2027, 3, 2, 12, 0, 0)
            };

            // Act
            ServiceResult<EventDto> result = await service.CreateEventAsync(dto);

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.Validation, error.Type);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task CreateEvent_ReturnsValidationError_WhenTotalSeatsIsInvalid(int? totalSeats)
        {
            // Arrange
            AppDbContext context = _database.Context;
            IEventService service = _database.EventService;
            CreateEventDto dto = new CreateEventDto
            {
                Title = "Новая встреча",
                StartAt = new DateTime(2027, 3, 2, 10, 0, 0),
                EndAt = new DateTime(2027, 3, 2, 12, 0, 0),
                TotalSeats = totalSeats
            };

            // Act
            ServiceResult<EventDto> result = await service.CreateEventAsync(dto);

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.Validation, error.Type);
            Assert.Empty(context.Events);
        }

        [Fact]
        public async Task UpdateEvent_PreservesSeatCounts_WhenEventExists()
        {
            // Arrange
            Event existingEvent = CreateStoredEvent(
                "Существующая встреча",
                new DateTime(2027, 3, 3, 10, 0, 0),
                new DateTime(2027, 3, 3, 12, 0, 0));
            Assert.True(existingEvent.TryReserveSeats(3));
            await CreateContextAsync(existingEvent);
            IEventService service = _database.EventService;
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Обновлённая встреча",
                StartAt = new DateTime(2027, 3, 3, 13, 0, 0),
                EndAt = new DateTime(2027, 3, 3, 15, 0, 0)
            };

            // Act
            ServiceResult<EventDto> result = await service.UpdateEventAsync(existingEvent.Id, dto);

            // Assert
            Assert.True(result.Success);
            EventDto updatedEvent = Assert.IsType<EventDto>(result.Data);
            Assert.Equal(10, updatedEvent.TotalSeats);
            Assert.Equal(7, updatedEvent.AvailableSeats);
        }

        [Fact]
        public async Task UpdateEvent_ReturnsValidationError_WhenEndDateIsBeforeStartDate()
        {
            // Arrange
            Event existingEvent = CreateStoredEvent(
                "Существующая встреча",
                new DateTime(2027, 3, 3, 10, 0, 0),
                new DateTime(2027, 3, 3, 12, 0, 0));
            await CreateContextAsync(existingEvent);
            IEventService service = _database.EventService;
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Обновлённая встреча",
                StartAt = new DateTime(2027, 3, 3, 14, 0, 0),
                EndAt = new DateTime(2027, 3, 3, 13, 0, 0)
            };

            // Act
            ServiceResult<EventDto> result = await service.UpdateEventAsync(existingEvent.Id, dto);

            // Assert
            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.Validation, error.Type);
        }

        [Fact]
        public async Task UpdateEvent_PreservesBookings_WhenNavigationIsLoaded()
        {
            // Arrange
            Event existingEvent = CreateStoredEvent(
                "Существующая встреча",
                new DateTime(2030, 1, 1, 10, 0, 0),
                new DateTime(2030, 1, 1, 12, 0, 0));
            Assert.True(existingEvent.TryReserveSeats());
            AppDbContext context = await CreateContextAsync(existingEvent);
            Event trackedEvent = await context.Events.SingleAsync(TestContext.Current.CancellationToken);
            Booking booking = new Booking(existingEvent.Id);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            IEventService service = _database.EventService;
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Новое название",
                StartAt = existingEvent.StartAt.AddHours(1),
                EndAt = existingEvent.EndAt.AddHours(1)
            };

            // Act
            ServiceResult<EventDto> result = await service.UpdateEventAsync(existingEvent.Id, dto);

            // Assert
            Assert.True(result.Success);
            Assert.Same(booking, Assert.Single(trackedEvent.Bookings));
            context.ChangeTracker.Clear();
            Event storedEvent = await context.Events.Include(entity => entity.Bookings)
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(dto.Title, storedEvent.Title);
            Assert.Equal(9, storedEvent.AvailableSeats);
            Assert.Equal(booking.Id, Assert.Single(storedEvent.Bookings).Id);
        }

        private async Task<AppDbContext> CreateContextAsync(params Event[] events)
        {
            await _database.SeedAsync(events);
            return _database.Context;
        }

        public void Dispose() => _database.Dispose();

        private static Event CreateStoredEvent(string title, DateTime startAt, DateTime endAt)
        {
            return new Event(Guid.NewGuid(), title, null, startAt, endAt, 10);
        }
    }
}
