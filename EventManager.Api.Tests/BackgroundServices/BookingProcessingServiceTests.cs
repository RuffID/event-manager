using EventManager.Api.BackgroundServices;
using EventManager.Api.Models;
using EventManager.Api.Tests.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            CancellableBookingProcessingDelay processingDelay =
                new CancellableBookingProcessingDelay();
            using ServiceTestContext database = new ServiceTestContext(processingDelay);
            Event @event = Event.Create("Тестовое событие", null,
                new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), 1);
            await database.SeedAsync(@event);
            await database.BookingService.CreateBookingAsync(@event.Id, cancellationToken: TestContext.Current.CancellationToken);
            using BookingProcessingService service = new BookingProcessingService(
                database.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
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
            database.Context.ChangeTracker.Clear();
            Booking storedBooking = await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(BookingStatus.Pending, storedBooking.Status);
            Assert.Null(storedBooking.ProcessedAt);
        }

        [Fact]
        public async Task ExecuteAsync_ContinuesProcessing_WhenBatchScopeCreationFails()
        {
            // Arrange
            CancellableBookingProcessingDelay processingDelay = new CancellableBookingProcessingDelay();
            using ServiceTestContext database = new ServiceTestContext(processingDelay);
            Event @event = Event.Create("Событие", null, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 1);
            await database.SeedAsync(@event);
            await database.BookingService.CreateBookingAsync(@event.Id, cancellationToken: TestContext.Current.CancellationToken);
            FailingOnceScopeFactory scopeFactory = new FailingOnceScopeFactory(
                database.ServiceProvider.GetRequiredService<IServiceScopeFactory>());
            using BookingProcessingService service = new BookingProcessingService(
                scopeFactory, NullLogger<BookingProcessingService>.Instance);
            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act
            try
            {
                await service.StartAsync(CancellationToken.None);
                await processingDelay.Started.WaitAsync(timeout.Token);
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }

            // Assert
            Assert.True(scopeFactory.CallCount >= 2);
            Assert.NotNull(service.ExecuteTask);
            Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
        }

        private class FailingOnceScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
        {
            private int _callCount;

            public int CallCount => Volatile.Read(ref _callCount);

            public IServiceScope CreateScope()
            {
                if (Interlocked.Increment(ref _callCount) == 1)
                    throw new InvalidOperationException("Scope creation failed.");

                return inner.CreateScope();
            }
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
