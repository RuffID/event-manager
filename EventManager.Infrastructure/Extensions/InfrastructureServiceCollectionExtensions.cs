using EventManager.Infrastructure.DataAccess;
using EventManager.Application.Abstractions.Persistence;
using EventManager.Infrastructure.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventManager.Infrastructure.Extensions
{
    /// <summary>Подключает хранение данных, безопасность и реализации прикладных портов.</summary>
    public static class InfrastructureServiceCollectionExtensions
    {
        /// <summary>Регистрирует PostgreSQL, общую scoped-единицу работы и компоненты безопасности.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <param name="configuration">Внешняя конфигурация подключения.</param>
        /// <returns>Коллекция сервисов с инфраструктурными регистрациями.</returns>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
            }

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
            services.AddSecurityComponents(configuration);

            return services.AddRepositories();
        }

        /// <summary>Регистрирует репозитории и сохранение через уже зарегистрированный scoped-контекст.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с реализациями прикладных портов.</returns>
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
