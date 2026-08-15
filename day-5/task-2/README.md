# Day 5 — Task 2: Container image from `dotnet publish` (no Dockerfile)

`QuotesApi` copied unchanged (logic-wise) from `day-1/QuotesApi` — the simple
quotes CRUD API (SQLite + EF Core, no auth). No Dockerfile anywhere in this folder;
the image is built entirely by .NET 10's built-in container publishing.

## What changed vs. day-1

- `QuotesApi.csproj`: added the three container properties (below).
- `appsettings.json`: `QuotesDb` connection string points at `/tmp/quotes.db`
  instead of a relative `quotes.db`. The ASP.NET container base image runs as a
  non-root user that can't write to `/app`, so the DB file needs a writable path.
- `Program.cs`: added a real `GET /health` endpoint that checks
  `db.Database.CanConnectAsync()` — it exercises the actual `QuotesDbContext`,
  not a hardcoded response.

## Container configuration (`QuotesApi/QuotesApi.csproj`)

```xml
<ContainerImageName>quotes-api</ContainerImageName>
<ContainerImageTag>0.1.0</ContainerImageTag>
<ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0-alpine</ContainerBaseImage>
```

## Publishing the image

```bash
cd QuotesApi
dotnet publish --os linux-musl --arch x64 /t:PublishContainer
```

Note: `linux-musl`, not plain `linux`. The base image
(`mcr.microsoft.com/dotnet/aspnet:10.0-alpine`) is musl-libc (Alpine), and the
SQLite native library is libc-specific — publishing with plain `--os linux`
produces a glibc build of `libe_sqlite3.so` that fails to load (and, in an
Alpine container, crashes the app) at startup. `linux-musl` is what .NET's own
docs specify for Alpine targets; it produces the correct native asset and the
build still resolves `ContainerBaseImage` to the alpine tag above with no other
changes.

## Running it

```bash
docker run -p 8080:8080 quotes-api:0.1.0
```

```bash
curl http://localhost:8080/health
# {"status":"healthy"}

curl http://localhost:8080/api/quotes
# {"page":1,"size":10,"total":0,"items":[]}
```

On Apple Silicon this image runs under Docker Desktop's x86-64 emulation, so
first-request latency is much higher than a native run — give it 60-90 seconds
after `docker run` before the first `curl`.

## Verification actually performed

- `dotnet build` — succeeded, 0 warnings, 0 errors.
- `dotnet publish --os linux-musl --arch x64 /t:PublishContainer` — succeeded,
  produced and pushed `quotes-api:0.1.0` to the local Docker registry.
- `docker run -p 8080:8080 quotes-api:0.1.0` — container started and stayed up.
- `curl http://localhost:8080/health` → `{"status":"healthy"}`.
- `curl -X POST http://localhost:8080/api/quotes -d '{"author":"...","text":"..."}'`
  followed by `curl http://localhost:8080/api/quotes` — confirmed the created
  quote round-trips through SQLite inside the container.

No test project exists for this API (`day-1/QuotesApi` doesn't have one either),
so there are no automated tests to run here.
