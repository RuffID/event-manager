using EventManager.Api.DataAccess;

namespace EventManager.Api.Extensions
{
    /// <summary>Содержит методы инициализации приложения.</summary>
    public static class WebApplicationExtensions
    {
        /// <summary>Создаёт базу данных и схему, если они ещё не существуют.</summary>
        /// <param name="app">Приложение с зарегистрированным контекстом базы данных.</param>
        public static void InitializeDatabase(this WebApplication app)
        {
            using IServiceScope scope = app.Services.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureCreated();
        }
    }
}
