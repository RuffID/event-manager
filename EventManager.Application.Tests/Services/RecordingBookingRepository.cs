using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Entities;

namespace EventManager.Application.Tests.Services;

internal class RecordingBookingRepository : IBookingRepository
{
    public Booking? Booking { get; set; }
    public Booking? Added { get; private set; }
    public IReadOnlyList<Guid>? PendingIds { get; set; }
    public List<(string Operation, Guid Id, CancellationToken Token)> Calls { get; } = [];

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(("read", id, cancellationToken));

        return Task.FromResult(Booking);
    }

    public Task<Booking?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(("update", id, cancellationToken));

        return Task.FromResult(Booking);
    }

    public Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken)
    {
        if (PendingIds is null)
        {
            throw new NotSupportedException("The scenario must not enumerate pending bookings.");
        }

        Calls.Add(("pending", Guid.Empty, cancellationToken));

        return Task.FromResult(PendingIds);
    }

    public void Add(Booking booking)
    {
        Added = booking;
    }
}
