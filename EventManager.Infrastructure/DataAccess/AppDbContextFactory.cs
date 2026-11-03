using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EventManager.Infrastructure.DataAccess
{
    /// <summary>Создаёт контекст для EF tooling без запуска приложения и применения миграций.</summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        /// <summary>Настраивает провайдер PostgreSQL из внешней конфигурации.</summary>
        /// <param name="args">Аргументы EF tooling; configuration-directory задаёт каталог appsettings.json.</param>
        /// <returns>Контекст для построения модели и генерации миграций.</returns>
        public AppDbContext CreateDbContext(string[] args)
        {
            IConfiguration arguments = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();
            string? configurationDirectory = arguments["configuration-directory"];
            if (configurationDirectory is null)
            {
                configurationDirectory = Directory.GetCurrentDirectory();
            }

            if (string.IsNullOrWhiteSpace(configurationDirectory))
            {
                throw new ArgumentException("Configuration directory must not be empty.", nameof(args));
            }

            string configurationPath = Path.GetFullPath(configurationDirectory);
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(configurationPath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
            }

            DbContextOptionsBuilder<AppDbContext> optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
