using EventManager.Api.DataAccess;
using EventManager.Api.Models;
using Xunit;

namespace EventManager.Api.IntegrationTests.Infrastructure;

public static class TestData
{
    public static Event CreateEvent(string title = "Test event", DateTime? startAt = null,
        DateTime? endAt = null, string? description = "Description", int seats = 10)
    {
        DateTime start = DateTime.UtcNow.Date.AddDays(10);
        if (startAt.HasValue)
        {
            start = startAt.Value;
        }

        DateTime end = start.AddHours(2);
        if (endAt.HasValue)
        {
            end = endAt.Value;
        }

        return Event.Create(title, description, start, end, seats);
    }

    public static async Task SeedAsync(PostgresFixture database, Event[] events, params Booking[] bookings)
    {
        await using AppDbContext context = database.CreateContext();

        context.Events.AddRange(events);
        context.Bookings.AddRange(bookings);
        await new EfUnitOfWork(context).SaveChangesAsync(CancellationToken.None);
    }

    public static void AssertEvent(Event expected, Event actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.StartAt, actual.StartAt);
        Assert.Equal(expected.EndAt, actual.EndAt);
        Assert.Equal(expected.TotalSeats, actual.TotalSeats);
        Assert.Equal(expected.AvailableSeats, actual.AvailableSeats);
    }

    public static void AssertBooking(Booking expected, Booking actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.EventId, actual.EventId);
        Assert.Equal(expected.Status, actual.Status);
        AssertTime(expected.CreatedAt, actual.CreatedAt);
        if (expected.ProcessedAt.HasValue)
        {
            Assert.NotNull(actual.ProcessedAt);
            AssertTime(expected.ProcessedAt.Value, actual.ProcessedAt.Value);
        }
        else
        {
            Assert.Null(actual.ProcessedAt);
        }
    }

    private static void AssertTime(DateTime expected, DateTime actual)
    {
        // PostgreSQL и .NET могут хранить время с разной точностью.
        Assert.InRange(actual, expected.AddMilliseconds(-1), expected.AddMilliseconds(1));
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
    }
}
