using EventManager.Domain.Entities;
using EventManager.Domain.Exceptions;
using Xunit;

namespace EventManager.Application.Tests.Models;

public class EventBookingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ReserveSeat_AtOrAfterStartThrowsWithoutChangingSeats(int elapsedSeconds)
    {
        // Arrange
        DateTime start = DateTime.UtcNow.AddDays(1);
        Event existing = Event.Create("Event", null, start, start.AddHours(1), 2);

        // Act
        Action action = () => existing.ReserveSeat(start.AddSeconds(elapsedSeconds));

        // Assert
        Assert.Throws<EventAlreadyStartedException>(action);
        Assert.Equal(2, existing.AvailableSeats);
    }

    [Fact]
    public void ReserveSeat_BeforeStartReservesOneSeat()
    {
        // Arrange
        DateTime start = DateTime.UtcNow.AddDays(1);
        Event existing = Event.Create("Event", null, start, start.AddHours(1), 2);

        // Act
        existing.ReserveSeat(start.AddTicks(-1));

        // Assert
        Assert.Equal(1, existing.AvailableSeats);
    }
}
