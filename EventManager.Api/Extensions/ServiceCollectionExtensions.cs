using EventManager.Api.BackgroundServices;
using EventManager.Api.Repositories;
using EventManager.Api.Services;

namespace EventManager.Api.Extensions
{
    /// <summary>
    /// Содержит методы расширения для регистрации сервисов приложения.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>Регистрирует сервисы и хранилища приложения в контейнере зависимостей.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с добавленными регистрациями.</returns>
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddSingleton<InMemoryEventRepository>();
            services.AddSingleton<InMemoryBookingRepository>();
            services.AddSingleton<IBookingProcessingDelay, BookingProcessingDelay>();
            services.AddSingleton<BookingProcessor>();
            services.AddHostedService<BookingProcessingService>();

            return services;
        }
    }
}
