using System.Diagnostics;
using ChangeTrackerDemo.Data;
using ChangeTrackerDemo.Models;
using Microsoft.EntityFrameworkCore;

const string connectionString = "Data Source=changetracker.db";
const int rowCount = 10_000;
const int authorCount = 25;

DbContextOptions<TrackerDbContext> BuildOptions() =>
    new DbContextOptionsBuilder<TrackerDbContext>()
        .UseSqlite(connectionString)
        .Options;

await using (var db = new TrackerDbContext(BuildOptions()))
{
    await db.Database.EnsureCreatedAsync();

    if (!await db.Quotes.AnyAsync())
    {
        Console.WriteLine($"Seeding {rowCount} quotes across {authorCount} authors...");

        var authors = Enumerable.Range(1, authorCount)
            .Select(i => new Author { Name = $"Author {i}" })
            .ToList();
        db.Authors.AddRange(authors);
        await db.SaveChangesAsync();

        var quotes = Enumerable.Range(1, rowCount)
            .Select(i => new Quote
            {
                Text = $"Quote number {i}",
                AuthorId = authors[i % authorCount].Id
            });
        db.Quotes.AddRange(quotes);
        await db.SaveChangesAsync();
    }
}

async Task<List<Quote>> RunTrackedAsync()
{
    await using var db = new TrackerDbContext(BuildOptions());
    return await db.Quotes.Include(q => q.Author).Take(rowCount).ToListAsync();
}

async Task<List<Quote>> RunNoTrackingAsync()
{
    await using var db = new TrackerDbContext(BuildOptions());
    return await db.Quotes.Include(q => q.Author).AsNoTracking().Take(rowCount).ToListAsync();
}

async Task<(double ms, long bytes)> MeasureAsync(Func<Task> action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var allocBefore = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    await action();
    sw.Stop();
    var allocAfter = GC.GetAllocatedBytesForCurrentThread();

    return (sw.Elapsed.TotalMilliseconds, allocAfter - allocBefore);
}

// --- Timing / allocation comparison ---
// One warm-up run per variant first, so JIT/query-plan compilation and SQLite's page
// cache don't get counted as "tracking overhead". Then a few measured runs, averaged.
const int measuredRuns = 3;

Console.WriteLine();
Console.WriteLine("=== Warm-up (discarded) ===");
await MeasureAsync(async () => await RunTrackedAsync());
await MeasureAsync(async () => await RunNoTrackingAsync());

Console.WriteLine();
Console.WriteLine("=== Tracked query (Quotes.Include(Author).Take(10_000)) ===");
var trackedRuns = new List<(double ms, long bytes)>();
for (var i = 1; i <= measuredRuns; i++)
{
    List<Quote>? result = null;
    var (ms, bytes) = await MeasureAsync(async () => result = await RunTrackedAsync());
    trackedRuns.Add((ms, bytes));
    Console.WriteLine($"  run {i}: {ms:F1} ms, {bytes / 1024.0:F0} KB allocated, rows: {result!.Count}");
}

Console.WriteLine();
Console.WriteLine("=== AsNoTracking query (Quotes.Include(Author).AsNoTracking().Take(10_000)) ===");
var noTrackingRuns = new List<(double ms, long bytes)>();
for (var i = 1; i <= measuredRuns; i++)
{
    List<Quote>? result = null;
    var (ms, bytes) = await MeasureAsync(async () => result = await RunNoTrackingAsync());
    noTrackingRuns.Add((ms, bytes));
    Console.WriteLine($"  run {i}: {ms:F1} ms, {bytes / 1024.0:F0} KB allocated, rows: {result!.Count}");
}

var trackedAvgMs = trackedRuns.Average(r => r.ms);
var trackedAvgKb = trackedRuns.Average(r => r.bytes) / 1024.0;
var noTrackingAvgMs = noTrackingRuns.Average(r => r.ms);
var noTrackingAvgKb = noTrackingRuns.Average(r => r.bytes) / 1024.0;

Console.WriteLine();
Console.WriteLine("=== Averages over 3 measured runs ===");
Console.WriteLine($"  Tracked:      {trackedAvgMs:F1} ms, {trackedAvgKb:F0} KB");
Console.WriteLine($"  AsNoTracking: {noTrackingAvgMs:F1} ms, {noTrackingAvgKb:F0} KB");
Console.WriteLine($"  Difference:   {trackedAvgMs - noTrackingAvgMs:F1} ms, {trackedAvgKb - noTrackingAvgKb:F0} KB");

// --- Identity resolution demo ---
Console.WriteLine();
Console.WriteLine("=== Identity resolution ===");

await using (var db = new TrackerDbContext(BuildOptions()))
{
    var quotes = await db.Quotes.Include(q => q.Author).Where(q => q.AuthorId == 1).Take(2).ToListAsync();
    var sameInstance = ReferenceEquals(quotes[0].Author, quotes[1].Author);
    Console.WriteLine($"Tracked: two quotes by the same author share one Author instance -> {sameInstance}");
    Console.WriteLine($"Tracked: ChangeTracker.Entries().Count() = {db.ChangeTracker.Entries().Count()}");
}

await using (var db = new TrackerDbContext(BuildOptions()))
{
    var quotes = await db.Quotes.Include(q => q.Author).AsNoTracking()
        .Where(q => q.AuthorId == 1).Take(2).ToListAsync();
    var sameInstance = ReferenceEquals(quotes[0].Author, quotes[1].Author);
    Console.WriteLine($"AsNoTracking: two quotes by the same author share one Author instance -> {sameInstance}");
    Console.WriteLine($"AsNoTracking: ChangeTracker.Entries().Count() = {db.ChangeTracker.Entries().Count()}");
}

await using (var db = new TrackerDbContext(BuildOptions()))
{
    var quotes = await db.Quotes.Include(q => q.Author).AsNoTrackingWithIdentityResolution()
        .Where(q => q.AuthorId == 1).Take(2).ToListAsync();
    var sameInstance = ReferenceEquals(quotes[0].Author, quotes[1].Author);
    Console.WriteLine($"AsNoTrackingWithIdentityResolution: two quotes by the same author share one Author instance -> {sameInstance}");
    Console.WriteLine($"AsNoTrackingWithIdentityResolution: ChangeTracker.Entries().Count() = {db.ChangeTracker.Entries().Count()}");
}
