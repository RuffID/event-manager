using Xunit;

namespace EventManager.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(NAME, DisableParallelization = true)]
public class PostgreSqlCollection : ICollectionFixture<PostgresFixture>
{
    public const string NAME = "PostgreSQL integration";
}
