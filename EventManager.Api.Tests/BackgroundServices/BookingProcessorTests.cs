using EventManager.Api.BackgroundServices;
using EventManager.Api.Models;
using EventManager.Api.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManager.Api.Tests.BackgroundServices
{
    public class BookingProcessorTests
    {
        [Fact]
        public async Task ProcessPendingBookingsAsync_ConfirmsAndStoresBooking_WhenBookingIsPending()
        {
            InMemoryBookingRepository repository = new InMemoryBookingRepository();
            Booking booking = new Booking(Guid.NewGuid());
            repository.Bookings.TryAdd(booking.Id, booking);
            BookingProcessor processor = CreateProcessor(
                repository,
                new ImmediateBookingProcessingDelay());

            await processor.ProcessPendingBookingsAsync(CancellationToken.None);

            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
            Assert.Same(booking, repository.Bookings[booking.Id]);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_ThrowsAndLeavesBookingPending_WhenProcessingFails()
        {
            InMemoryBookingRepository repository = new InMemoryBookingRepository();
            Booking booking = new Booking(Guid.NewGuid());
            repository.Bookings.TryAdd(booking.Id, booking);
            BookingProcessor processor = CreateProcessor(
                repository,
                new FailingBookingProcessingDelay());

            Task action = processor.ProcessPendingBookingsAsync(CancellationToken.None);

            await Assert.ThrowsAsync<ApplicationException>(() => action);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Null(booking.ProcessedAt);
        }

        [Fact]
        public async Task ProcessPendingBookingsAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
        {
            InMemoryBookingRepository repository = new InMemoryBookingRepository();
            Booking booking = new Booking(Guid.NewGuid());
            repository.Bookings.TryAdd(booking.Id, booking);
            BookingProcessor processor = CreateProcessor(
                repository,
                new ImmediateBookingProcessingDelay());
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Task action = processor.ProcessPendingBookingsAsync(cancellationTokenSource.Token);

            await Assert.ThrowsAsync<OperationCanceledException>(() => action);
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Null(booking.ProcessedAt);
        }

        private static BookingProcessor CreateProcessor(
            InMemoryBookingRepository repository,
            IBookingProcessingDelay processingDelay)
        {
            return new BookingProcessor(
                repository,
                processingDelay,
                NullLogger<BookingProcessor>.Instance);
        }

        private class ImmediateBookingProcessingDelay : IBookingProcessingDelay
        {
            public Task WaitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private class FailingBookingProcessingDelay : IBookingProcessingDelay
        {
            public Task WaitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromException(new ApplicationException("Processing failed."));
            }
        }
    }
}
