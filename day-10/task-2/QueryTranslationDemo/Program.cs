using QueryTranslationDemo.Data;
using QueryTranslationDemo.Dtos;
using QueryTranslationDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

const string connectionString = "Data Source=quotes.db";

// LogTo + EnableSensitiveDataLogging so the actual generated SQL (with parameter
// values) shows up in the console. Only meant for this kind of local demo/dev work,
// not something to ship with sensitive data logging turned on in production.
DbContextOptions<QuoteDbContext> BuildOptions() =>
    new DbContextOptionsBuilder<QuoteDbContext>()
        .UseSqlite(connectionString)
        .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information)
        .EnableSensitiveDataLogging()
        .Options;

await using (var db = new QuoteDbContext(BuildOptions()))
{
    await db.Database.EnsureCreatedAsync();

    if (!await db.Quotes.AnyAsync())
    {
        Console.WriteLine("Seeding quotes...");
        db.Quotes.AddRange(
            new Quote { Text = "Simplicity is the ultimate sophistication.", AuthorName = "Leonardo da Vinci", Category = "Design", Amount = 120m, Notes = "Licensed for the Q1 newsletter." },
            new Quote { Text = "Talk is cheap. Show me the code.", AuthorName = "Linus Torvalds", Category = "Engineering", Amount = 40m, Notes = "Used in the onboarding deck." },
            new Quote { Text = "Premature optimization is the root of all evil.", AuthorName = "Donald Knuth", Category = "Engineering", Amount = 150m, Notes = "Requested by marketing for the blog." },
            new Quote { Text = "Any fool can write code a computer understands.", AuthorName = "Martin Fowler", Category = "Engineering", Amount = 90m, Notes = "Pending legal review." },
            new Quote { Text = "The best way to predict the future is to invent it.", AuthorName = "Alan Kay", Category = "Vision", Amount = 200m, Notes = "Used twice already this year." },
            new Quote { Text = "Make it work, make it right, make it fast.", AuthorName = "Kent Beck", Category = "Engineering", Amount = 30m, Notes = "Low-cost internal use only." }
        );
        await db.SaveChangesAsync();
    }
}

// --- 1. Full entity query ---
Console.WriteLine();
Console.WriteLine("=== 1. Full entity query: db.Quotes.Where(q => q.Amount > 50) ===");
await using (var db = new QuoteDbContext(BuildOptions()))
{
    var quotes = await db.Quotes.Where(q => q.Amount > 50).ToListAsync();
    Console.WriteLine($"-> {quotes.Count} quotes loaded, each with all {typeof(Quote).GetProperties().Length} columns (including Notes, which nothing here needs).");
}

// --- 2. Projected DTO query ---
Console.WriteLine();
Console.WriteLine("=== 2. Projected query: .Select(q => new QuoteDto { ... }) ===");
await using (var db = new QuoteDbContext(BuildOptions()))
{
    var dtos = await db.Quotes
        .Where(q => q.Amount > 50)
        .Select(q => new QuoteDto { Id = q.Id, Text = q.Text, Amount = q.Amount })
        .ToListAsync();
    Console.WriteLine($"-> {dtos.Count} QuoteDto loaded, only Id/Text/Amount come back from the DB.");
}

// --- 3. Accidental client-side evaluation (the bug) ---
Console.WriteLine();
Console.WriteLine("=== 3. BEFORE: accidental client-side filtering ===");
await using (var db = new QuoteDbContext(BuildOptions()))
{
    // Bug: ToListAsync() runs first with no Where, so EF pulls every row into memory,
    // and the Amount > 100 filter below is plain LINQ-to-Objects over that in-memory list.
    var allQuotes = await db.Quotes.ToListAsync();
    var expensive = allQuotes.Where(q => q.Amount > 100).ToList();
    Console.WriteLine($"-> pulled {allQuotes.Count} full rows from SQLite, then filtered down to {expensive.Count} in memory.");
}

// --- 4. Corrected: filter stays on the database side ---
Console.WriteLine();
Console.WriteLine("=== 4. AFTER: filter pushed back into the query ===");
await using (var db = new QuoteDbContext(BuildOptions()))
{
    var expensive = await db.Quotes.Where(q => q.Amount > 100).ToListAsync();
    Console.WriteLine($"-> SQLite only returns the {expensive.Count} matching rows this time.");
}
