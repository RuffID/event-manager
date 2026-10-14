using EventManager.Api.DataAccess;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace EventManager.Api.IntegrationTests.Infrastructure;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("event_manager_integration_tests")
        .WithUsername("integration_user")
        .WithPassword("integration_password")
        .Build();

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
        }
        catch
        {
            await _postgres.DisposeAsync();
            throw;
        }
    }

    public AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    public async Task ResetAsync()
    {
        await DeleteDatabaseAsync();
        await using AppDbContext context = CreateContext();

        if (!context.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException("В сборке отсутствуют миграции AppDbContext.");
        }

        await context.Database.MigrateAsync(CancellationToken.None);
    }

    public async Task DeleteDatabaseAsync()
    {
        await using AppDbContext context = CreateContext();

        await context.Database.EnsureDeletedAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
