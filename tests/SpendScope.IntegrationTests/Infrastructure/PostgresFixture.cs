using Testcontainers.PostgreSql;

namespace SpendScope.IntegrationTests.Infrastructure;

/// <summary>One PostgreSQL 16 container shared by every test in the "Postgres" collection.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("spendscope")
        .WithUsername("spendscope")
        .WithPassword("integration-tests")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresTestGroup : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
