using EventManager.Api.BackgroundServices;
using EventManager.Infrastructure.DataAccess;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventManager.Api.Tests.Services;

public class ServiceTestContextTests
{
    [Fact]
    public void Constructor_ReplacesProductionDelayWithSingleTestDouble()
    {
        // Arrange
        ImmediateDelay delay = new ImmediateDelay();
        using ServiceTestContext database = new ServiceTestContext(delay);

        // Act
        IBookingProcessingDelay resolved = database.ServiceProvider.GetRequiredService<IBookingProcessingDelay>();
        IBookingProcessingDelay[] registrations = database.ServiceProvider.GetServices<IBookingProcessingDelay>().ToArray();

        // Assert
        Assert.Same(delay, resolved);
        Assert.Same(delay, Assert.Single(registrations));
    }

    [Fact]
    public void Dispose_ReleasesScopedContextAndRootProvider()
    {
        // Arrange
        ServiceTestContext database = new ServiceTestContext();
        AppDbContext context = database.Context;

        // Act
        database.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => context.Model);
        Assert.Throws<ObjectDisposedException>(() => database.ServiceProvider.CreateScope());
    }

    private class ImmediateDelay : IBookingProcessingDelay
    {
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }
}
