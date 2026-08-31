using EventManager.Api.Models;
using Xunit;

namespace EventManager.Api.Tests.Models
{
    public class BookingTests
    {
        [Fact]
        public void Constructor_CreatesPendingBooking_WhenEventIdIsValid()
        {
            Guid eventId = Guid.NewGuid();
            DateTime beforeCreation = DateTime.UtcNow;

            Booking booking = new Booking(eventId);

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
            Action action = () => new Booking(Guid.Empty);

            ArgumentException exception = Assert.Throws<ArgumentException>(action);

            Assert.Equal("eventId", exception.ParamName);
        }

        [Fact]
        public void Confirm_SetsConfirmedStatusAndProcessedAt_WhenBookingIsPending()
        {
            Booking booking = new Booking(Guid.NewGuid());
            DateTime beforeProcessing = DateTime.UtcNow;

            booking.Confirm();

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
            Booking booking = new Booking(Guid.NewGuid());
            DateTime beforeProcessing = DateTime.UtcNow;

            booking.Reject();

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
            Booking booking = new Booking(Guid.NewGuid());
            booking.Confirm();
            DateTime? processedAt = booking.ProcessedAt;

            Action action = booking.Reject;

            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
        }

        [Fact]
        public void Confirm_ThrowsInvalidOperationException_WhenBookingIsRejected()
        {
            Booking booking = new Booking(Guid.NewGuid());
            booking.Reject();
            DateTime? processedAt = booking.ProcessedAt;

            Action action = booking.Confirm;

            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Rejected, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
        }
    }
}
