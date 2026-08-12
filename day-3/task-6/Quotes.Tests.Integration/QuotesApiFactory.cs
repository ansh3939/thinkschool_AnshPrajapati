using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuotesApi.Clock;
using QuotesApi.Data;

namespace Quotes.Tests.Integration;

/// <summary>
/// Boots the real QuotesApi pipeline in-memory. Only the database and the clock
/// are swapped out; authentication, authorization and endpoint routing stay real.
/// Create one factory per test so every test gets its own database.
/// </summary>
public class QuotesApiFactory : WebApplicationFactory<Program>
{
    // SQLite keeps an in-memory database alive only while a connection to it is open,
    // so this connection is opened here and stays open until the factory is disposed.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public QuotesApiFactory() => _connection.Open();

    /// <summary>The fake clock the app resolves as <see cref="IClock"/>; tests can move it.</summary>
    public FakeClock Clock { get; } = new(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Point the app's DbContext at this test's in-memory SQLite connection.
            services.RemoveAll<DbContextOptions<QuotesDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<QuotesDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });

        // Program.cs applies EF migrations and seeds during startup, so building the
        // host below fails the test if the migrations cannot be applied.
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
