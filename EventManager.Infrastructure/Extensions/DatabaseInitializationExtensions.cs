using EventManager.Api.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventManager.Infrastructure.Extensions
{
    /// <summary>Содержит инфраструктурную инициализацию хранилища при запуске.</summary>
    public static class DatabaseInitializationExtensions
    {
        /// <summary>Применяет миграции в отдельном scope и пробрасывает ошибку для остановки запуска.</summary>
        /// <param name="services">Корневой поставщик сервисов построенного приложения.</param>
        public static void ApplyDatabaseMigrations(this IServiceProvider services)
        {
            using (IServiceScope scope = services.CreateScope())
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Database.Migrate();
            }
        }
    }
}
