# Day 10 — Task 1: EF Core change tracker + AsNoTracking

## What I did

Built a small console app (`ChangeTrackerDemo/`) that seeds a SQLite database with
10,000 `Quote` rows spread across 25 `Author`s, then reads all 10,000 rows two ways —
once with EF Core's normal change tracking, once with `AsNoTracking()` — and times both
with `Stopwatch` and `GC.GetAllocatedBytesForCurrentThread()`. It also shows how EF's
identity resolution works (or doesn't) depending on which one you use.

Reused the `QuotesDbContext`/`Quote` shape from `day-5/task-1/QuotesApi` as the starting
point (plain-POCO models, primary-constructor `DbContext`, SQLite via `UseSqlite`), just
renamed to `TrackerDbContext`/`Quote`/`Author` and added the `Author` navigation property
since the identity-resolution part needs a related entity that repeats across rows.

## The two query variants

Tracked (default):

```csharp
async Task<List<Quote>> RunTrackedAsync()
{
    await using var db = new TrackerDbContext(BuildOptions());
    return await db.Quotes.Include(q => q.Author).Take(rowCount).ToListAsync();
}
```

No-tracking:

```csharp
async Task<List<Quote>> RunNoTrackingAsync()
{
    await using var db = new TrackerDbContext(BuildOptions());
    return await db.Quotes.Include(q => q.Author).AsNoTracking().Take(rowCount).ToListAsync();
}
```

Same query, same 10,000 rows, only the `AsNoTracking()` call differs.

## Timing / allocation results

Each variant gets one warm-up run (discarded, so JIT and SQLite's page cache don't get
counted as "tracking overhead"), then 3 measured runs on a fresh `DbContext` each time
(so the change tracker isn't carrying over state from the previous run). Numbers below
are from an actual run on my machine:

```
=== Tracked query (Quotes.Include(Author).Take(10_000)) ===
  run 1: 237.3 ms, 12555 KB allocated, rows: 10000
  run 2: 167.6 ms, 12555 KB allocated, rows: 10000
  run 3: 149.6 ms, 12555 KB allocated, rows: 10000

=== AsNoTracking query (Quotes.Include(Author).AsNoTracking().Take(10_000)) ===
  run 1: 73.1 ms, 9313 KB allocated, rows: 10000
  run 2: 60.5 ms, 9313 KB allocated, rows: 10000
  run 3: 69.1 ms, 9313 KB allocated, rows: 10000

=== Averages over 3 measured runs ===
  Tracked:      184.8 ms, 12555 KB
  AsNoTracking: 67.6 ms, 9313 KB
  Difference:   117.2 ms, 3243 KB
```

Tracked averaged ~185 ms / ~12,555 KB, AsNoTracking averaged ~68 ms / ~9,313 KB — so on
this run, AsNoTracking was about 2.7x faster and allocated about 26% less.

I re-ran this a few times while writing it and the absolute milliseconds moved around a
fair bit (I saw the tracked run anywhere from ~150 ms up to several seconds, depending on
what else was running on my machine — VS Code's C# language server and MSBuild were both
alive in the background eating CPU). The allocation numbers, though, came back identical
(12,555 KB / 9,313 KB) on every run — allocation isn't affected by scheduling noise the
way wall-clock time is, so it's the more trustworthy number here. Whatever the absolute
timings were on a given run, tracked was consistently slower and consistently allocated
more than AsNoTracking, by a similar ratio each time. The gap comes from real work: for
every one of the 10,000 rows, the tracked path snapshots the original property values,
creates an internal `InternalEntityEntry`, and runs it through the change tracker's
identity map so future `SaveChanges` calls know what changed. AsNoTracking skips all of
that and just materializes the objects.

## Identity resolution

```
Tracked: two quotes by the same author share one Author instance -> True
Tracked: ChangeTracker.Entries().Count() = 3
AsNoTracking: two quotes by the same author share one Author instance -> False
AsNoTracking: ChangeTracker.Entries().Count() = 0
AsNoTrackingWithIdentityResolution: two quotes by the same author share one Author instance -> True
AsNoTrackingWithIdentityResolution: ChangeTracker.Entries().Count() = 0
```

- **Tracked:** when two `Quote`s in the same query have the same `AuthorId`, EF's change
  tracker recognizes the second one as "an entity I already have" and hands back the
  exact same `Author` object instead of building a second one — that's identity
  resolution, and it's a side effect of every loaded entity going through the change
  tracker's identity map. `ChangeTracker.Entries()` shows 3 entries for the 2 quotes + 1
  shared author.
- **AsNoTracking:** there's no change tracker involved at all, so there's no identity map
  to check against — each row is materialized independently, so the two `Quote`s end up
  with two separate `Author` objects even though they represent the same author row.
  `ChangeTracker.Entries()` is empty.
- **AsNoTrackingWithIdentityResolution():** the middle ground — it still doesn't track
  entities for `SaveChanges` (`ChangeTracker.Entries()` is still empty), but it keeps a
  temporary identity map just for the duration of that query, so repeated references to
  the same row still resolve to the same object. Useful when you're read-only but the
  result graph has enough duplication (like this `Author` fan-out) that you don't want
  redundant object copies.

## Exercise answer

**Tracked query:**
```csharp
await db.Quotes.Include(q => q.Author).Take(10_000).ToListAsync();
```

**AsNoTracking query:**
```csharp
await db.Quotes.Include(q => q.Author).AsNoTracking().Take(10_000).ToListAsync();
```

**Timing/allocation difference (measured, 3-run average):** tracked ~184.8 ms / 12,555 KB
vs AsNoTracking ~67.6 ms / 9,313 KB — AsNoTracking was ~2.7x faster and allocated ~26%
less for reading these 10,000 rows.

**When NOT to use AsNoTracking:** don't use it when you're going to modify the entities
and call `SaveChanges()` afterward — without tracking, EF has no original values to diff
against, so it won't know what changed.

## What I learned this session

The change tracker isn't free even when you don't touch it after loading — just reading
rows with tracking on costs real time and real allocations, because EF still has to
snapshot every entity for a `SaveChanges()` you might never call. I also hadn't
appreciated that "no tracking" and "no identity resolution" are two separate things EF
lets you opt in/out of independently — `AsNoTrackingWithIdentityResolution()` exists
specifically for the case where you want deduplicated object references in a read-only
result but don't want the change-tracking overhead.

## What would break this

- Seeding logic only runs `if (!await db.Quotes.AnyAsync())`, so if the row shape or seed
  count ever changes, the old `changetracker.db` file needs to be deleted first or the
  demo will silently keep reading whatever was seeded before.
- The timing numbers are wall-clock on a shared dev machine with other tools running, so
  the exact milliseconds aren't reproducible — the relative gap (tracked slower, more
  allocation) is the part that held up across repeated runs, not the absolute numbers.
- If the two `Quote` rows picked for the identity-resolution check (`Take(2)` on
  `AuthorId == 1`) ever seeded with fewer than 2 quotes for that author, the
  `quotes[0]`/`quotes[1]` indexing would throw — with 400 quotes per author out of 25
  authors, that's not a real risk here, but it's not defensively guarded.

## Reproducing this

```bash
cd ChangeTrackerDemo
dotnet run
```

First run seeds `changetracker.db` (gitignored) with 10,000 rows; subsequent runs reuse
it. Delete `changetracker.db` to reseed from scratch.
