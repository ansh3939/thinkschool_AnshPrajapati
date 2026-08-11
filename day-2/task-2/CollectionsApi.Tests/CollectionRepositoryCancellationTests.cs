using System.Data.Common;
using CollectionsApi.Data;
using CollectionsApi.Models;
using CollectionsApi.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CollectionsApi.Tests;

public class CollectionRepositoryCancellationTests
{
    [Fact]
    public async Task GetById_cancels_an_in_progress_database_operation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var interceptor = new BlockingQueryInterceptor();
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        await using (var setupContext = new CollectionsDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Collections.Add(new Collection(ownerId: 1, name: "Reading list"));
            await setupContext.SaveChangesAsync();
        }

        await using var db = new CollectionsDbContext(options);
        var repository = new CollectionRepository(db);
        using var cancellationSource = new CancellationTokenSource();

        var getCollectionTask = repository.GetById(1, cancellationSource.Token);
        await interceptor.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => getCollectionTask);
    }

    private sealed class BlockingQueryInterceptor : DbCommandInterceptor
    {
        public TaskCompletionSource QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return result;

            QueryStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return result;
        }
    }
}
