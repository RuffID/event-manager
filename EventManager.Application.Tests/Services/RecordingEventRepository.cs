using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Entities;

namespace EventManager.Application.Tests.Services;

internal class RecordingEventRepository : IEventRepository
{
    public Event? Event { get; set; }
    public EventPage Page { get; set; } = new EventPage([], 0);
    public Event? Added { get; private set; }
    public Event? Removed { get; private set; }
    public Action? BeforeReturningTrackedEvent { get; set; }
    public List<(string Operation, Guid Id, CancellationToken Token)> Calls { get; } = [];
    public (string? Title, DateTime? From, DateTime? To, int Page, int PageSize)? PageArguments { get; private set; }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(("read", id, cancellationToken));

        return Task.FromResult(Event);
    }

    public Task<Event?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(("update", id, cancellationToken));
        BeforeReturningTrackedEvent?.Invoke();

        return Task.FromResult(Event);
    }

    public Task<Event?> GetCurrentByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(("current", id, cancellationToken));

        return Task.FromResult(Event);
    }

    public Task<EventPage> GetPageAsync(string? title, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        Calls.Add(("page", Guid.Empty, cancellationToken));
        PageArguments = (title, from, to, page, pageSize);

        return Task.FromResult(Page);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(("exists", id, cancellationToken));

        return Task.FromResult(Event is not null);
    }

    public void Add(Event @event)
    {
        Added = @event;
    }

    public void Remove(Event @event)
    {
        Removed = @event;
    }
}
