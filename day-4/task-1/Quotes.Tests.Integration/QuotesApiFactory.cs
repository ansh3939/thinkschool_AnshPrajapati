using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuotesApi.Clock;
using QuotesApi.Data;

namespace Quotes.Tests.Integration;

/// <summary>
/// Boots the real QuotesApi pipeline against the Testcontainers SQL Server. Only the
/// database and the clock are swapped out; authentication, authorization and endpoint
/// routing stay real. Create one factory per test: each factory gets its own database
/// on the shared container, so tests never see each other's data.
/// </summary>
public class QuotesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public QuotesApiFactory(SqlServerContainerFixture sqlServer)
    {
        var builder = new SqlConnectionStringBuilder(sqlServer.ConnectionString)
        {
            InitialCatalog = $"QuotesTest_{Guid.NewGuid():N}"
        };
        _connectionString = builder.ConnectionString;
    }

    /// <summary>The fake clock the app resolves as <see cref="IClock"/>; tests can move it.</summary>
    public FakeClock Clock { get; } = new(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Point the app's DbContext at this test's own database on the Testcontainers SQL Server.
            services.RemoveAll<DbContextOptions<QuotesDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<QuotesDbContext>(options => options.UseSqlServer(_connectionString));

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });

        // Program.cs applies EF migrations and seeds during startup, so building the
        // host below fails the test if the migrations cannot be applied. Since the
        // database name above is new, this creates and migrates it from scratch.
    }
}
