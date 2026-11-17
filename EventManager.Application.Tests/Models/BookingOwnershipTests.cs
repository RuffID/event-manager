using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Xunit;

namespace EventManager.Application.Tests.Models
{
    public class BookingOwnershipTests
    {
        [Fact]
        public void Constructor_CreatesOwnedPendingBooking_WhenIdentifiersAreValid()
        {
            // Arrange
            Guid eventId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();

            // Act
            Booking booking = new Booking(eventId, userId);

            // Assert
            Assert.NotEqual(Guid.Empty, booking.Id);
            Assert.Equal(eventId, booking.EventId);
            Assert.Equal(userId, booking.UserId);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Null(booking.ProcessedAt);
        }

        [Fact]
        public void Constructor_ThrowsArgumentException_WhenUserIdIsEmpty()
        {
            // Arrange
            Guid eventId = Guid.NewGuid();

            // Act
            Action action = () => new Booking(eventId, Guid.Empty);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("userId", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsArgumentException_WhenEventIdIsEmpty()
        {
            // Arrange
            Guid userId = Guid.NewGuid();

            // Act
            Action action = () => new Booking(Guid.Empty, userId);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("eventId", exception.ParamName);
        }
    }
}
