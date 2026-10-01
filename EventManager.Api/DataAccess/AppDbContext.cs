using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Api.DataAccess
{
    /// <summary>Предоставляет доступ к событиям и бронированиям в базе данных.</summary>
    public class AppDbContext : DbContext
    {
        /// <summary>Создаёт контекст с указанными параметрами подключения.</summary>
        /// <param name="options">Параметры контекста базы данных.</param>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>Получает набор событий.</summary>
        public DbSet<Event> Events => Set<Event>();

        /// <summary>Получает набор бронирований.</summary>
        public DbSet<Booking> Bookings => Set<Booking>();

        /// <summary>Подключает конфигурации сущностей из сборки приложения.</summary>
        /// <param name="modelBuilder">Построитель модели базы данных.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
