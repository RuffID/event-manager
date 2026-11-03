using EventManager.Application.Abstractions.Persistence;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using EventManager.Domain.Entities;
using Xunit;

namespace EventManager.Api.Tests.Services;

public class EventServicePortTests
{
    [Fact]
    public async Task CreateEvent_AddsEntityAndSavesWithCallerToken_BeforeReturningDto()
    {
        // Arrange
        RecordingEventRepository events = new RecordingEventRepository();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        EventService service = new EventService(events, work);
        CreateEventDto input = CreateInput();
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        ServiceResult<EventDto> result = await service.CreateEventAsync(input, cancellation.Token);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Event added = Assert.IsType<Event>(events.Added);
        EventDto dto = Assert.IsType<EventDto>(result.Data);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(added.Id, dto.Id);
        Assert.Equal(input.Title, dto.Title);
        Assert.Equal(input.Description, dto.Description);
        Assert.Equal(input.StartAt, dto.StartAt);
        Assert.Equal(input.EndAt, dto.EndAt);
        Assert.Equal(input.TotalSeats, dto.TotalSeats);
        Assert.Equal(input.TotalSeats, dto.AvailableSeats);
        Assert.Equal(cancellation.Token, Assert.Single(work.SavedTokens));
    }

    [Fact]
    public async Task UpdateEvent_UsesTrackedEntityAndSavesWithCallerToken()
    {
        // Arrange
        Event existing = CreateEvent();
        Assert.True(existing.TryReserveSeats());
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        EventService service = new EventService(events, work);
        UpdateEventDto input = UpdateInput(existing);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        ServiceResult<EventDto> result = await service.UpdateEventAsync(existing.Id, input, cancellation.Token);

        // Assert
        Assert.True(result.Success);
        EventDto dto = Assert.IsType<EventDto>(result.Data);
        Assert.Equal(existing.Id, dto.Id);
        Assert.Equal(input.Title, dto.Title);
        Assert.Equal(input.Description, dto.Description);
        Assert.Equal(input.StartAt, dto.StartAt);
        Assert.Equal(input.EndAt, dto.EndAt);
        Assert.Equal(2, dto.TotalSeats);
        Assert.Equal(1, dto.AvailableSeats);
        Assert.Equal(("update", existing.Id, cancellation.Token), Assert.Single(events.Calls));
        Assert.Null(events.Added);
        Assert.Equal(cancellation.Token, Assert.Single(work.SavedTokens));
    }

    [Fact]
    public async Task DeleteEvent_RemovesTrackedEntityAndSavesWithCallerToken()
    {
        // Arrange
        Event existing = CreateEvent();
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        EventService service = new EventService(events, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        ServiceResult result = await service.DeleteEventAsync(existing.Id, cancellation.Token);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Same(existing, events.Removed);
        Assert.Equal(("update", existing.Id, cancellation.Token), Assert.Single(events.Calls));
        Assert.Equal(cancellation.Token, Assert.Single(work.SavedTokens));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task MissingEvent_ReturnsNotFoundWithoutSaving(string operation)
    {
        // Arrange
        RecordingEventRepository events = new RecordingEventRepository();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        EventService service = new EventService(events, work);
        Event missing = CreateEvent();
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        bool success;
        ServiceError? error;
        if (operation == "delete")
        {
            ServiceResult result = await service.DeleteEventAsync(missing.Id, cancellation.Token);
            success = result.Success;
            error = result.Error;
        }
        else
        {
            ServiceResult<EventDto> result = operation switch
            {
                "get" => await service.GetEventByIdAsync(missing.Id, cancellation.Token),
                "update" => await service.UpdateEventAsync(missing.Id, UpdateInput(missing), cancellation.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            success = result.Success;
            error = result.Error;
        }

        // Assert
        Assert.False(success);
        Assert.Equal(ServiceErrorType.NotFound, Assert.IsType<ServiceError>(error).Type);
        Assert.Null(events.Added);
        Assert.Null(events.Removed);
        Assert.Empty(work.SavedTokens);
        Assert.Equal(missing.Id, Assert.Single(events.Calls).Id);
        Assert.Equal(cancellation.Token, Assert.Single(events.Calls).Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidInput_DoesNotAddRemoveOrSave(bool update)
    {
        // Arrange
        Event existing = CreateEvent();
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        EventService service = new EventService(events, work);
        CreateEventDto create = CreateInput();
        create.Title = " ";
        UpdateEventDto change = UpdateInput(existing);
        change.Title = " ";

        // Act
        ServiceResult<EventDto> result;
        if (update)
        {
            result = await service.UpdateEventAsync(existing.Id, change, TestContext.Current.CancellationToken);
        }
        else
        {
            result = await service.CreateEventAsync(create, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Validation, Assert.IsType<ServiceError>(result.Error).Type);
        Assert.Equal("Original", existing.Title);
        Assert.Null(events.Added);
        Assert.Null(events.Removed);
        Assert.Empty(events.Calls);
        Assert.Empty(work.SavedTokens);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task SaveFailure_PropagatesInsteadOfReturningSuccess(string operation)
    {
        // Arrange
        Event existing = CreateEvent();
        RecordingEventRepository events = new RecordingEventRepository { Event = existing };
        InvalidOperationException failure = new InvalidOperationException("Storage unavailable.");
        RecordingUnitOfWork work = new RecordingUnitOfWork { SaveError = failure };
        EventService service = new EventService(events, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        Func<Task> action = operation switch
        {
            "create" => () => service.CreateEventAsync(CreateInput(), cancellation.Token),
            "update" => () => service.UpdateEventAsync(existing.Id, UpdateInput(existing), cancellation.Token),
            "delete" => () => service.DeleteEventAsync(existing.Id, cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        // Assert
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(action));
        Assert.Equal(cancellation.Token, Assert.Single(work.SavedTokens));
    }

    [Fact]
    public async Task Reads_ForwardFiltersAndTokenWithoutSaving()
    {
        // Arrange
        Event existing = CreateEvent();
        RecordingEventRepository events = new RecordingEventRepository
        {
            Event = existing,
            Page = new EventPage([existing], 7)
        };
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        EventService service = new EventService(events, work);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        ServiceResult<EventDto> single = await service.GetEventByIdAsync(existing.Id, cancellation.Token);
        PaginatedResult page = await service.GetEventsAsync("Original", existing.StartAt, existing.EndAt,
            2, 3, cancellation.Token);

        // Assert
        Assert.True(single.Success);
        Assert.Equal(existing.Id, Assert.IsType<EventDto>(single.Data).Id);
        Assert.Equal(existing.Id, Assert.Single(page.Items).Id);
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.PageSize);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(("Original", existing.StartAt, existing.EndAt, 2, 3), events.PageArguments);
        Assert.Equal(new[] { ("read", existing.Id, cancellation.Token), ("page", Guid.Empty, cancellation.Token) }, events.Calls);
        Assert.Empty(work.SavedTokens);
    }

    private static CreateEventDto CreateInput()
    {
        DateTime startAt = DateTime.UtcNow.AddDays(2);

        return new CreateEventDto
        {
            Title = "Created", Description = "Description", StartAt = startAt,
            EndAt = startAt.AddHours(2), TotalSeats = 2
        };
    }

    private static Event CreateEvent()
    {
        DateTime startAt = DateTime.UtcNow.AddDays(1);

        return Event.Create("Original", null, startAt, startAt.AddHours(2), 2);
    }

    private static UpdateEventDto UpdateInput(Event existing)
    {
        return new UpdateEventDto
        {
            Title = "Updated", Description = "Changed", StartAt = existing.StartAt.AddHours(1),
            EndAt = existing.EndAt.AddHours(1)
        };
    }
}
