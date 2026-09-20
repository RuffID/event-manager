using EventManager.Api.Models;
using Xunit;

namespace EventManager.Api.Tests.Models
{
    public class BookingTests
    {
        [Fact]
        public void Constructor_CreatesPendingBooking_WhenEventIdIsValid()
        {
            // Arrange
            Guid eventId = Guid.NewGuid();
            DateTime beforeCreation = DateTime.UtcNow;

            // Act
            Booking booking = new Booking(eventId);

            // Assert
            DateTime afterCreation = DateTime.UtcNow;
            Assert.NotEqual(Guid.Empty, booking.Id);
            Assert.Equal(eventId, booking.EventId);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.InRange(booking.CreatedAt, beforeCreation, afterCreation);
            Assert.Equal(DateTimeKind.Utc, booking.CreatedAt.Kind);
            Assert.Null(booking.ProcessedAt);
        }

        [Fact]
        public void Constructor_ThrowsArgumentException_WhenEventIdIsEmpty()
        {
            // Arrange

            // Act
            Action action = () => new Booking(Guid.Empty);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("eventId", exception.ParamName);
        }

        [Fact]
        public void Confirm_SetsConfirmedStatusAndProcessedAt_WhenBookingIsPending()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid());
            DateTime beforeProcessing = DateTime.UtcNow;

            // Act
            booking.Confirm();

            // Assert
            DateTime afterProcessing = DateTime.UtcNow;
            Assert.NotNull(booking.ProcessedAt);
            DateTime processedAt = booking.ProcessedAt.Value;
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.InRange(processedAt, beforeProcessing, afterProcessing);
            Assert.Equal(DateTimeKind.Utc, processedAt.Kind);
        }

        [Fact]
        public void Reject_SetsRejectedStatusAndProcessedAt_WhenBookingIsPending()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid());
            DateTime beforeProcessing = DateTime.UtcNow;

            // Act
            booking.Reject();

            // Assert
            DateTime afterProcessing = DateTime.UtcNow;
            Assert.NotNull(booking.ProcessedAt);
            DateTime processedAt = booking.ProcessedAt.Value;
            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.InRange(processedAt, beforeProcessing, afterProcessing);
            Assert.Equal(DateTimeKind.Utc, processedAt.Kind);
        }

        [Fact]
        public void Reject_ThrowsInvalidOperationException_WhenBookingIsConfirmed()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid());
            booking.Confirm();
            DateTime? processedAt = booking.ProcessedAt;

            // Act
            Action action = booking.Reject;

            // Assert
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
        }

        [Fact]
        public void Confirm_ThrowsInvalidOperationException_WhenBookingIsRejected()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid());
            booking.Reject();
            DateTime? processedAt = booking.ProcessedAt;

            // Act
            Action action = booking.Confirm;

            // Assert
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
        }

        [Fact]
        public void Confirm_ThrowsInvalidOperationException_WhenBookingIsAlreadyConfirmed()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid());
            booking.Confirm();
            DateTime? processedAt = booking.ProcessedAt;

            // Act
            Action action = booking.Confirm;

            // Assert
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
        }

        [Fact]
        public void Reject_ThrowsInvalidOperationException_WhenBookingIsAlreadyRejected()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid());
            booking.Reject();
            DateTime? processedAt = booking.ProcessedAt;

            // Act
            Action action = booking.Reject;

            // Assert
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
        }

        [Fact]
        public async Task ConfirmAndReject_AllowOnlyOneTransition_WhenCalledConcurrently()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid());

            // Act
            Task<Exception?> confirmTask = Task.Run(() => Record.Exception(booking.Confirm));
            Task<Exception?> rejectTask = Task.Run(() => Record.Exception(booking.Reject));

            Exception?[] exceptions = await Task.WhenAll(confirmTask, rejectTask);

            // Assert
            Assert.Equal(1, exceptions.Count(exception => exception is null));
            Assert.Equal(1, exceptions.Count(exception => exception is InvalidOperationException));
            Assert.True(
                booking.Status is BookingStatus.Confirmed or BookingStatus.Rejected);
            Assert.NotNull(booking.ProcessedAt);
        }
    }
}
