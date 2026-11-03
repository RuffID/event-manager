using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Entities;

namespace EventManager.Api.Tests.Services;

internal class RecordingBookingRepository : IBookingRepository
{
    public Booking? Booking { get; set; }
    public Booking? Added { get; private set; }
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
        throw new NotSupportedException("The scenario must not enumerate pending bookings.");
    }

    public void Add(Booking booking)
    {
        Added = booking;
    }
}
