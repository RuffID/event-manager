using EventManager.Api.BackgroundServices;
using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Api.Extensions
{
    /// <summary>
    /// Содержит методы расширения для регистрации сервисов приложения.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>Регистрирует контекст базы данных с провайдером PostgreSQL.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <param name="configuration">Конфигурация приложения.</param>
        /// <returns>Коллекция сервисов с зарегистрированным контекстом.</returns>
        public static IServiceCollection AddDataAccess(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            string? connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
            }

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

            return services.AddRepositories();
        }

        /// <summary>Регистрирует репозитории и общую границу сохранения в текущем scope.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с добавленными регистрациями доступа к данным.</returns>
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }

        /// <summary>Регистрирует задержку, координатор и хостинг фоновой обработки бронирований.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с добавленными регистрациями.</returns>
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddSingleton<IBookingProcessingDelay, BookingProcessingDelay>();
            services.AddSingleton<BookingProcessor>();
            services.AddHostedService<BookingProcessingService>();

            return services;
        }
    }
}
