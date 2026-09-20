using EventManager.Api.Models;
using EventManager.Api.Repositories;
using Xunit;

namespace EventManager.Api.Tests.Repositories
{
    public class InMemoryBookingRepositoryTests
    {
        [Fact]
        public void Constructor_CreatesEmptyRepository()
        {
            // Arrange

            // Act
            InMemoryBookingRepository repository = new InMemoryBookingRepository();

            // Assert
            Assert.Empty(repository.Bookings);
        }

        [Fact]
        public void Bookings_ReturnsStoredBooking_WhenBookingWasAdded()
        {
            // Arrange
            InMemoryBookingRepository repository = new InMemoryBookingRepository();
            Booking booking = new Booking(Guid.NewGuid());

            // Act
            bool added = repository.Bookings.TryAdd(booking.Id, booking);

            // Assert
            Assert.True(added);
            Booking storedBooking = Assert.IsType<Booking>(repository.Bookings[booking.Id]);
            Assert.Same(booking, storedBooking);
        }

        [Fact]
        public void Bookings_RejectsDuplicateIdentifier_WhenBookingWasAlreadyAdded()
        {
            // Arrange
            InMemoryBookingRepository repository = new InMemoryBookingRepository();
            Booking booking = new Booking(Guid.NewGuid());
            repository.Bookings.TryAdd(booking.Id, booking);

            // Act
            bool addedAgain = repository.Bookings.TryAdd(booking.Id, booking);

            // Assert
            Assert.False(addedAgain);
            Assert.Single(repository.Bookings);
        }
    }
}
