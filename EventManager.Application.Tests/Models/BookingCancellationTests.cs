using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Xunit;

namespace EventManager.Application.Tests.Models
{
    public class BookingCancellationTests
    {
        [Fact]
        public void Cancel_SetsCancelledStatus_WhenBookingIsPending()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid(), Guid.NewGuid());

            // Act
            booking.Cancel();

            // Assert
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Null(booking.ProcessedAt);
            Assert.Equal((BookingStatus.Cancelled, (DateTime?)null), booking.GetProcessingState());
        }

        [Fact]
        public void Cancel_PreservesProcessedAt_WhenBookingIsConfirmed()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid(), Guid.NewGuid());
            booking.Confirm();
            DateTime? processedAt = booking.ProcessedAt;

            // Act
            booking.Cancel();

            // Assert
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
            Assert.Equal((BookingStatus.Cancelled, processedAt), booking.GetProcessingState());
        }

        [Fact]
        public void Cancel_ThrowsInvalidOperationException_WhenBookingIsAlreadyCancelled()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid(), Guid.NewGuid());
            booking.Confirm();
            booking.Cancel();
            DateTime? processedAt = booking.ProcessedAt;

            // Act
            Action action = booking.Cancel;

            // Assert
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Equal(processedAt, booking.ProcessedAt);
        }

        [Fact]
        public void Confirm_ThrowsInvalidOperationException_WhenBookingIsCancelled()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid(), Guid.NewGuid());
            booking.Cancel();

            // Act
            Action action = booking.Confirm;

            // Assert
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Null(booking.ProcessedAt);
        }

        [Fact]
        public void Reject_ThrowsInvalidOperationException_WhenBookingIsCancelled()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid(), Guid.NewGuid());
            booking.Cancel();

            // Act
            Action action = booking.Reject;

            // Assert
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Null(booking.ProcessedAt);
        }

        [Fact]
        public async Task Cancel_AllowsOnlyOneCancellation_WhenCalledConcurrently()
        {
            // Arrange
            Booking booking = new Booking(Guid.NewGuid(), Guid.NewGuid());

            // Act
            Task<Exception?> firstTask = Task.Run(() => Record.Exception(booking.Cancel));
            Task<Exception?> secondTask = Task.Run(() => Record.Exception(booking.Cancel));
            Exception?[] exceptions = await Task.WhenAll(firstTask, secondTask);

            // Assert
            Assert.Single(exceptions, exception => exception is null);
            Assert.Single(exceptions, exception => exception is InvalidOperationException);
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Null(booking.ProcessedAt);
        }
    }
}
