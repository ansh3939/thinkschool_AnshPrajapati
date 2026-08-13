using Testcontainers.MsSql;

namespace Quotes.Tests.Integration;

/// <summary>
/// Starts one SQL Server 2022 container for the whole test run and stops it when the run finishes.
/// Individual tests get their own database on this container via <see cref="QuotesApiFactory"/>.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SQL Server collection";
}
