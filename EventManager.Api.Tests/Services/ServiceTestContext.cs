using EventManager.Api.DataAccess;
using EventManager.Api.Models;
using EventManager.Api.Services;
using EventManager.Api.BackgroundServices;
using EventManager.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventManager.Api.Tests.Services
{
    internal class ServiceTestContext : IDisposable
    {
        private readonly IServiceScope _scope;

        public ServiceTestContext(IBookingProcessingDelay? processingDelay = null)
        {
            string databaseName = Guid.NewGuid().ToString();
            ServiceCollection services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddLogging();
            services.AddServices();

            if (processingDelay is not null)
                services.AddSingleton(processingDelay);
            ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
            _scope = ServiceProvider.CreateScope();
        }

        public ServiceProvider ServiceProvider { get; }

        public AppDbContext Context => _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        public IEventService EventService => _scope.ServiceProvider.GetRequiredService<IEventService>();

        public IBookingService BookingService => _scope.ServiceProvider.GetRequiredService<IBookingService>();

        public BookingProcessor BookingProcessor => ServiceProvider.GetRequiredService<BookingProcessor>();

        public async Task SeedAsync(params Event[] events)
        {
            Context.Events.AddRange(events);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public void Dispose()
        {
            _scope.Dispose();
            ServiceProvider.Dispose();
        }
    }
}
