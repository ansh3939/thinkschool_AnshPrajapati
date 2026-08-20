# Day 10 — Task 2: query translation + projections

## What I did

Small console app (`QueryTranslationDemo/`) with a `Quotes` table in SQLite. Each
`Quote` has `Id`, `Text`, `AuthorName`, `Category`, `Amount` (a licensing fee for using
that quote) and `Notes` (an internal-only field, e.g. who requested it) — six columns,
so pulling the whole entity vs. just what an API consumer needs actually shows up as a
different SQL statement.

Reused the same setup as `day-10/task-1` (SQLite via `UseSqlite`, primary-constructor
`DbContext`, top-level `Program.cs`), just a new `Quote`/`QuoteDto` shape for this
exercise.

SQL is captured with:

```csharp
new DbContextOptionsBuilder<QuoteDbContext>()
    .UseSqlite(connectionString)
    .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information)
    .EnableSensitiveDataLogging()
    .Options;
```

`LogTo` with the `Database.Command` category prints every `Executed DbCommand` block
(SQL text + parameters) straight to the console. `EnableSensitiveDataLogging()` is what
makes the actual parameter values show up instead of just `@p0`, `@p1` etc. — fine for
this local demo, not something to leave on in a real app.

## 1 & 2. Full entity vs. projected

Original query, loads the whole `Quote`:

```csharp
var quotes = await db.Quotes.Where(q => q.Amount > 50).ToListAsync();
```

SQL EF generated (from `results.txt`):

```sql
SELECT "q"."Id", "q"."Amount", "q"."AuthorName", "q"."Category", "q"."Notes", "q"."Text"
FROM "Quotes" AS "q"
WHERE ef_compare("q"."Amount", '50.0') > 0
```

All 6 columns, including `Notes`, which nothing downstream of this actually reads.

Projected version:

```csharp
var dtos = await db.Quotes
    .Where(q => q.Amount > 50)
    .Select(q => new QuoteDto { Id = q.Id, Text = q.Text, Amount = q.Amount })
    .ToListAsync();
```

SQL:

```sql
SELECT "q"."Id", "q"."Text", "q"."Amount"
FROM "Quotes" AS "q"
WHERE ef_compare("q"."Amount", '50.0') > 0
```

Same `WHERE`, but the `SELECT` list drops from 6 columns to 3 — `AuthorName`,
`Category` and `Notes` never leave the database. On a table with a few columns like
this one it's not a big deal, but the same projection on a table with large text/blob
columns (which is exactly what `Notes` is standing in for here) is the difference
between the DB reading a few bytes per row vs. reading and shipping a chunk of text you
were going to throw away anyway.

(Side note: `ef_compare` shows up because SQLite has no native `decimal` type — EF's
Sqlite provider stores `Amount` as `TEXT` and uses that function to compare values
numerically instead of doing a lexical string comparison.)

## 3 & 4. The accidental client-side evaluation

Modern EF Core doesn't silently fall back to the client for stuff like this — if an
expression genuinely can't be translated inside `Where`/`Select`, it throws instead. The
actual footgun I hit while working through this wasn't a translation failure, it was
calling `ToListAsync()` before the filter instead of after:

```csharp
// BEFORE — bug
var allQuotes = await db.Quotes.ToListAsync();
var expensive = allQuotes.Where(q => q.Amount > 100).ToList();
```

SQL for that:

```sql
SELECT "q"."Id", "q"."Amount", "q"."AuthorName", "q"."Category", "q"."Notes", "q"."Text"
FROM "Quotes" AS "q"
```

No `WHERE` clause at all — every row in the table gets pulled into memory first, and
`Amount > 100` is just a plain C# `Where` running over that in-memory `List<Quote>`
afterwards. On 6 rows this is invisible; on a real table it means shipping the entire
table over the wire every time, then throwing most of it away in the app.

Fix — keep the filter inside the query so it becomes part of the `WHERE` clause:

```csharp
// AFTER — fixed
var expensive = await db.Quotes.Where(q => q.Amount > 100).ToListAsync();
```

```sql
SELECT "q"."Id", "q"."Amount", "q"."AuthorName", "q"."Category", "q"."Notes", "q"."Text"
FROM "Quotes" AS "q"
WHERE ef_compare("q"."Amount", '100.0') > 0
```

Now SQLite only returns the 3 rows that actually match, instead of all 6.

## Evidence

Full captured console output (all four SQL statements plus the seed/insert
statements) is in [`results.txt`](results.txt) in this folder.

## Exercise answer

- **Original query (whole entity):** `db.Quotes.Where(q => q.Amount > 50).ToListAsync()`
  → `SELECT "q"."Id", "q"."Amount", "q"."AuthorName", "q"."Category", "q"."Notes", "q"."Text" ...`
- **Projected query:** `.Select(q => new QuoteDto { Id = q.Id, Text = q.Text, Amount = q.Amount })`
  → `SELECT "q"."Id", "q"."Text", "q"."Amount" ...` (3 columns instead of 6)
- **Client-eval bug caught:** `ToListAsync()` before the `Where`, so the `Amount > 100`
  filter ran in memory over every row instead of in SQL.
- **Fix:** move `.Where(q => q.Amount > 100)` before `ToListAsync()`, so it's part of
  the translated query again.

## Reproducing this

```bash
cd QueryTranslationDemo
dotnet run
```

First run seeds `quotes.db` (gitignored via the repo's `*.db` rule) with 6 rows;
delete it to reseed. Every run prints the generated SQL for all four steps to the
console via `LogTo`.
