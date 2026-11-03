using EventManager.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventManager.Application.Extensions
{
    /// <summary>Регистрирует прикладные сценарии без выбора хранилища и запуска фонового хостинга.</summary>
    public static class ApplicationServiceCollectionExtensions
    {
        /// <summary>Регистрирует прикладные сервисы в текущем scope.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с прикладными регистрациями.</returns>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<BookingProcessing>();

            return services;
        }
    }
}
