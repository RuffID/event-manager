using System.ComponentModel.DataAnnotations;
using Xunit;
using Event = EventManager.Api.Models.Event;

namespace EventManager.Api.Tests.Models
{
    public class EventTests
    {
        [Fact]
        public void Create_InitializesAllSeatsAsAvailable_WhenTotalSeatsIsValid()
        {
            Event @event = CreateEvent(totalSeats: 10);

            Assert.Equal(10, @event.TotalSeats);
            Assert.Equal(10, @event.AvailableSeats);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_ThrowsValidationException_WhenTotalSeatsIsNotPositive(int totalSeats)
        {
            Action action = () => CreateEvent(totalSeats);

            Assert.Throws<ValidationException>(action);
        }

        [Fact]
        public void TryReserveSeats_DecreasesAvailableSeats_WhenEnoughSeatsAreAvailable()
        {
            Event @event = CreateEvent(totalSeats: 5);

            bool reserved = @event.TryReserveSeats(2);

            Assert.True(reserved);
            Assert.Equal(3, @event.AvailableSeats);
        }

        [Fact]
        public void TryReserveSeats_DoesNotChangeAvailableSeats_WhenSeatsAreInsufficient()
        {
            Event @event = CreateEvent(totalSeats: 2);

            bool reserved = @event.TryReserveSeats(3);

            Assert.False(reserved);
            Assert.Equal(2, @event.AvailableSeats);
        }

        [Fact]
        public void ReleaseSeats_IncreasesAvailableSeats_WhenSeatsWereReserved()
        {
            Event @event = CreateEvent(totalSeats: 5);
            Assert.True(@event.TryReserveSeats(2));

            @event.ReleaseSeats();

            Assert.Equal(4, @event.AvailableSeats);
        }

        [Fact]
        public void ReleaseSeats_ThrowsInvalidOperationException_WhenNoSeatsWereReserved()
        {
            Event @event = CreateEvent(totalSeats: 5);

            Action action = () => @event.ReleaseSeats();

            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(5, @event.AvailableSeats);
        }

        private static Event CreateEvent(int totalSeats)
        {
            return Event.Create(
                "Тестовое событие",
                null,
                new DateTime(2030, 1, 1, 10, 0, 0),
                new DateTime(2030, 1, 1, 12, 0, 0),
                totalSeats);
        }
    }
}
