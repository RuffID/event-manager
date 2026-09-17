using EventManager.Api.BackgroundServices;
using EventManager.Api.Models;
using EventManager.Api.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManager.Api.Tests.BackgroundServices
{
    public class BookingProcessingServiceTests
    {
        [Fact]
        public async Task StopAsync_CompletesExecution_WhenServiceIsCancelled()
        {
            // Arrange
            InMemoryBookingRepository bookingRepository = new InMemoryBookingRepository();
            InMemoryEventRepository eventRepository = new InMemoryEventRepository();
            Guid eventId = eventRepository.Events.Keys.First();
            Booking booking = new Booking(eventId);
            bookingRepository.Bookings.TryAdd(booking.Id, booking);
            CancellableBookingProcessingDelay processingDelay =
                new CancellableBookingProcessingDelay();
            BookingProcessor bookingProcessor = new BookingProcessor(
                bookingRepository,
                eventRepository,
                processingDelay,
                NullLogger<BookingProcessor>.Instance);
            using BookingProcessingService service = new BookingProcessingService(
                bookingProcessor,
                NullLogger<BookingProcessingService>.Instance);
            using CancellationTokenSource cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            await service.StartAsync(CancellationToken.None);
            await processingDelay.Started.WaitAsync(cancellationTokenSource.Token);
            await service.StopAsync(CancellationToken.None);

            // Assert
            Task? executeTask = service.ExecuteTask;
            Assert.NotNull(executeTask);
            Assert.True(executeTask.IsCompletedSuccessfully);
        }

        private class CancellableBookingProcessingDelay : IBookingProcessingDelay
        {
            private readonly TaskCompletionSource<bool> _started =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Started => _started.Task;

            public async Task WaitAsync(CancellationToken cancellationToken)
            {
                _started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }
}
