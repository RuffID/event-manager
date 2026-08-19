using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Repositories;
using EventManager.Api.Services;
using Xunit;
using Event = EventManager.Api.Models.Event;

namespace EventManager.Api.Tests.Services
{
    public class EventServiceTests
    {
        [Fact]
        public void CreateEvent_ReturnsCreatedEvent_WhenDataIsValid()
        {
            InMemoryEventRepository repository = CreateRepository();
            EventService service = new EventService(repository);
            CreateEventDto dto = new CreateEventDto
            {
                Title = "Новая встреча",
                Description = "Описание встречи",
                StartAt = new DateTime(2026, 11, 10, 18, 0, 0),
                EndAt = new DateTime(2026, 11, 10, 20, 0, 0)
            };

            ServiceResult<EventDto> result = service.CreateEvent(dto);

            Assert.True(result.Success);
            EventDto createdEvent = Assert.IsType<EventDto>(result.Data);
            Assert.Equal(dto.Title, createdEvent.Title);
            Assert.True(repository.Events.ContainsKey(createdEvent.Id));
        }

        [Fact]
        public void GetEvents_ReturnsAllEvents_WhenFiltersAreNotSpecified()
        {
            Event firstEvent = CreateStoredEvent(
                "Первая встреча",
                new DateTime(2026, 11, 1, 10, 0, 0),
                new DateTime(2026, 11, 1, 12, 0, 0));

            Event secondEvent = CreateStoredEvent(
                "Вторая встреча",
                new DateTime(2026, 11, 2, 10, 0, 0),
                new DateTime(2026, 11, 2, 12, 0, 0));

            InMemoryEventRepository repository = CreateRepository(firstEvent, secondEvent);
            EventService service = new EventService(repository);

            PaginatedResult result = service.GetEvents();

            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Events.Count);
            Assert.Contains(result.Events, item => item.Id == firstEvent.Id);
            Assert.Contains(result.Events, item => item.Id == secondEvent.Id);
        }

        [Fact]
        public void GetEventById_ReturnsEvent_WhenEventExists()
        {
            Event existingEvent = CreateStoredEvent(
                "Существующая встреча",
                new DateTime(2026, 11, 3, 10, 0, 0),
                new DateTime(2026, 11, 3, 12, 0, 0));

            InMemoryEventRepository repository = CreateRepository(existingEvent);
            EventService service = new EventService(repository);

            ServiceResult<EventDto> result = service.GetEventById(existingEvent.Id);

            Assert.True(result.Success);
            EventDto foundEvent = Assert.IsType<EventDto>(result.Data);
            Assert.Equal(existingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public void UpdateEvent_ReturnsUpdatedEvent_WhenEventExists()
        {
            Event existingEvent = CreateStoredEvent(
                "Старое название",
                new DateTime(2026, 11, 4, 10, 0, 0),
                new DateTime(2026, 11, 4, 12, 0, 0));

            InMemoryEventRepository repository = CreateRepository(existingEvent);
            EventService service = new EventService(repository);
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Новое название",
                Description = "Новое описание",
                StartAt = new DateTime(2026, 11, 4, 13, 0, 0),
                EndAt = new DateTime(2026, 11, 4, 15, 0, 0)
            };

            ServiceResult<EventDto> result = service.UpdateEvent(existingEvent.Id, dto);

            Assert.True(result.Success);
            EventDto updatedEvent = Assert.IsType<EventDto>(result.Data);
            Assert.Equal(dto.Title, updatedEvent.Title);
            Assert.Equal(dto.StartAt, updatedEvent.StartAt);
            Assert.Equal(dto.Title, repository.Events[existingEvent.Id].Title);
        }

        [Fact]
        public void DeleteEvent_RemovesEvent_WhenEventExists()
        {
            Event existingEvent = CreateStoredEvent(
                "Встреча для удаления",
                new DateTime(2026, 11, 5, 10, 0, 0),
                new DateTime(2026, 11, 5, 12, 0, 0));

            InMemoryEventRepository repository = CreateRepository(existingEvent);
            EventService service = new EventService(repository);

            ServiceResult result = service.DeleteEvent(existingEvent.Id);

            Assert.True(result.Success);
            Assert.False(repository.Events.ContainsKey(existingEvent.Id));
        }

        [Fact]
        public void GetEvents_ReturnsMatchingEvents_WhenTitleFilterIsSpecified()
        {
            Event matchingEvent = CreateStoredEvent(
                "C# Meetup",
                new DateTime(2026, 11, 6, 10, 0, 0),
                new DateTime(2026, 11, 6, 12, 0, 0));

            Event otherEvent = CreateStoredEvent(
                "Java Meetup",
                new DateTime(2026, 11, 7, 10, 0, 0),
                new DateTime(2026, 11, 7, 12, 0, 0));

            EventService service = new EventService(CreateRepository(matchingEvent, otherEvent));

            PaginatedResult result = service.GetEvents(title: "c# meet");

            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(matchingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public void GetEvents_ReturnsEventsInsideRange_WhenDateFiltersAreSpecified()
        {
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

            EventService service = new EventService(
                CreateRepository(matchingEvent, earlyEvent, lateEvent));

            PaginatedResult result = service.GetEvents(
                from: new DateTime(2026, 12, 5),
                to: new DateTime(2026, 12, 31, 23, 59, 59));

            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(matchingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public void GetEvents_ReturnsRequestedPage_WhenPaginationIsSpecified()
        {
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

            EventService service = new EventService(CreateRepository(
                firstEvent,
                secondEvent,
                thirdEvent,
                fourthEvent,
                fifthEvent));

            PaginatedResult result = service.GetEvents(page: 2, pageSize: 2);

            Assert.Equal(5, result.TotalCount);
            Assert.Equal(2, result.Page);
            Assert.Equal(2, result.PageSize);
            Guid[] expectedIds = [thirdEvent.Id, fourthEvent.Id];
            Assert.Equal(expectedIds, result.Events.Select(item => item.Id));
        }

        [Fact]
        public void GetEvents_ReturnsMatchingEvents_WhenFiltersAreCombined()
        {
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

            EventService service = new EventService(CreateRepository(
                matchingEvent,
                otherTitleEvent,
                earlyEvent,
                lateEvent));

            PaginatedResult result = service.GetEvents(
                title: "c#",
                from: new DateTime(2027, 2, 5),
                to: new DateTime(2027, 2, 28, 23, 59, 59));

            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(matchingEvent.Id, foundEvent.Id);
        }

        [Fact]
        public void GetEvents_ReturnsAllEvents_WhenTitleFilterContainsOnlyWhitespace()
        {
            Event firstEvent = CreateStoredEvent(
                "Первая встреча",
                new DateTime(2027, 2, 1, 10, 0, 0),
                new DateTime(2027, 2, 1, 12, 0, 0));

            Event secondEvent = CreateStoredEvent(
                "Вторая встреча",
                new DateTime(2027, 2, 2, 10, 0, 0),
                new DateTime(2027, 2, 2, 12, 0, 0));

            EventService service = new EventService(CreateRepository(firstEvent, secondEvent));

            PaginatedResult result = service.GetEvents(title: "   ");

            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Events.Count);
        }

        [Fact]
        public void GetEvents_IncludesEventsOnDateFilterBoundaries()
        {
            DateTime from = new DateTime(2027, 2, 10, 10, 0, 0);
            DateTime to = new DateTime(2027, 2, 10, 12, 0, 0);
            Event boundaryEvent = CreateStoredEvent("Граничная встреча", from, to);
            EventService service = new EventService(CreateRepository(boundaryEvent));

            PaginatedResult result = service.GetEvents(from: from, to: to);

            EventDto foundEvent = Assert.Single(result.Events);
            Assert.Equal(boundaryEvent.Id, foundEvent.Id);
        }

        [Fact]
        public void GetEventById_ReturnsNotFoundError_WhenEventDoesNotExist()
        {
            EventService service = new EventService(CreateRepository());

            ServiceResult<EventDto> result = service.GetEventById(Guid.NewGuid());

            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
        }

        [Fact]
        public void UpdateEvent_ReturnsNotFoundError_WhenEventDoesNotExist()
        {
            EventService service = new EventService(CreateRepository());
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Обновлённая встреча",
                StartAt = new DateTime(2027, 3, 1, 10, 0, 0),
                EndAt = new DateTime(2027, 3, 1, 12, 0, 0)
            };

            ServiceResult<EventDto> result = service.UpdateEvent(Guid.NewGuid(), dto);

            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
        }

        [Fact]
        public void DeleteEvent_ReturnsNotFoundError_WhenEventDoesNotExist()
        {
            EventService service = new EventService(CreateRepository());

            ServiceResult result = service.DeleteEvent(Guid.NewGuid());

            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.NotFound, error.Type);
        }

        [Fact]
        public void CreateEvent_ReturnsValidationError_WhenDataIsInvalid()
        {
            EventService service = new EventService(CreateRepository());
            CreateEventDto dto = new CreateEventDto
            {
                Title = " ",
                StartAt = new DateTime(2027, 3, 2, 10, 0, 0),
                EndAt = new DateTime(2027, 3, 2, 12, 0, 0)
            };

            ServiceResult<EventDto> result = service.CreateEvent(dto);

            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.Validation, error.Type);
        }

        [Fact]
        public void UpdateEvent_ReturnsValidationError_WhenEndDateIsBeforeStartDate()
        {
            Event existingEvent = CreateStoredEvent(
                "Существующая встреча",
                new DateTime(2027, 3, 3, 10, 0, 0),
                new DateTime(2027, 3, 3, 12, 0, 0));
            EventService service = new EventService(CreateRepository(existingEvent));
            UpdateEventDto dto = new UpdateEventDto
            {
                Title = "Обновлённая встреча",
                StartAt = new DateTime(2027, 3, 3, 14, 0, 0),
                EndAt = new DateTime(2027, 3, 3, 13, 0, 0)
            };

            ServiceResult<EventDto> result = service.UpdateEvent(existingEvent.Id, dto);

            Assert.False(result.Success);
            ServiceError error = Assert.IsType<ServiceError>(result.Error);
            Assert.Equal(ServiceErrorType.Validation, error.Type);
        }

        private static InMemoryEventRepository CreateRepository(params Event[] events)
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

        private static Event CreateStoredEvent(string title, DateTime startAt, DateTime endAt)
        {
            return new Event(Guid.NewGuid(), title, null, startAt, endAt);
        }
    }
}
