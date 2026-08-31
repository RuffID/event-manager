using EventManager.Api.BackgroundServices;
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
            using BookingProcessingService service = new BookingProcessingService(
                new InMemoryBookingRepository(),
                NullLogger<BookingProcessingService>.Instance);

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);

            Task? executeTask = service.ExecuteTask;
            Assert.NotNull(executeTask);
            Assert.True(executeTask.IsCompletedSuccessfully);
        }
    }
}
