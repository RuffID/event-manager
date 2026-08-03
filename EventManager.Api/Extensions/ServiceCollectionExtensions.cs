using EventManager.Api.Repositories;
using EventManager.Api.Services;

namespace EventManager.Api.Extensions
{
    /// <summary>
    /// Содержит методы расширения для регистрации сервисов приложения.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>Регистрирует сервис событий и хранилище событий в контейнере зависимостей.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с добавленными регистрациями.</returns>
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IEventService, EventService>();
            services.AddSingleton<InMemoryEventRepository>();

            return services;
        }
    }
}
