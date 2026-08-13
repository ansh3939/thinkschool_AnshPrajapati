# Day 4 — Task 5: OpenTelemetry tracing

Adds OpenTelemetry distributed tracing to the QuotesApi (copied from `day-4/task-4`, which
already had EF Core + JWT auth + Serilog). ASP.NET Core requests, EF Core queries, and
outbound HttpClient calls are all captured as spans and exported over OTLP to a local
Jaeger instance. Serilog logs are enriched with the real OpenTelemetry `TraceId` so a
request can be followed across both logs and traces.

## What changed vs. task-4

- Added OpenTelemetry packages and tracing configuration in `Program.cs`.
- Replaced the `TraceId` pushed into Serilog's `LogContext` — it used to be
  `HttpContext.TraceIdentifier` (an ASP.NET Core-only id), now it's
  `Activity.Current?.TraceId`, the actual OpenTelemetry trace id.
- Added `POST /api/quotes/import`, the smallest realistic endpoint that makes an outbound
  HTTP call: it fetches a random quote from the public ZenQuotes API and saves it via EF
  Core, so a single request demonstrates a nested HttpClient span and a nested EF Core span
  together.
- Wrapped the bcrypt password check in `POST /api/auth/login` with a custom
  `verify-password` span (`ActivitySource("QuotesApi")`), tagged with `user.id`. Password
  hashing is a genuinely CPU-heavy step that automatic instrumentation doesn't see.

## Packages added

- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.EntityFrameworkCore` (prerelease — this package has no
  stable release yet)
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP exporter)

## Tracing configuration

In `QuotesApi/Program.cs`:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("QuotesApi"))
    .WithTracing(tracing => tracing
        .AddSource(activitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));
```

The OTLP endpoint is read from `OpenTelemetry:OtlpEndpoint` in `appsettings.json`, defaulting
to `http://localhost:4317` (Jaeger's default OTLP/gRPC port).

## Running it

### 1. Start Jaeger

```bash
docker run -d --name quotesapi-jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  jaegertracing/all-in-one:latest
```

Jaeger's UI is now at http://localhost:16686 and it's listening for OTLP on 4317 (gRPC).

### 2. Start the API

```bash
cd day-4/task-5/QuotesApi
dotnet run --urls http://localhost:5301
```

On startup it applies EF Core migrations to a local SQLite file and seeds one user
(`test@example.com` / `password123`) and one quote.

### 3. Make requests that generate a trace

Log in (produces a request span, an EF Core span, and the custom `verify-password` span):

```bash
curl -X POST http://localhost:5301/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123"}'
```

Copy the `access_token` from the response, then import a quote (produces a request span, a
nested outbound HttpClient span, and a nested EF Core span):

```bash
curl -X POST http://localhost:5301/api/quotes/import \
  -H "Authorization: Bearer <access_token>"
```

### 4. Find the trace in Jaeger

Open http://localhost:16686, pick service `QuotesApi`, click "Find Traces". You'll see:

- `POST /api/auth/login` → `main` (EF Core query) and `verify-password` (custom span, tagged
  `user.id`) as siblings under the request span.
- `POST /api/quotes/import` → `GET` (outbound call to `zenquotes.io`) and `main` (EF Core
  insert) as siblings under the request span.

### 5. Confirm log/trace correlation

The console output from `dotnet run` logs each request via Serilog. Every log line for a
request carries the same `TraceId` property, and it matches the trace id shown in Jaeger for
that request (e.g. the URL Jaeger shows for a trace is `.../trace/<traceId>`).

## Limitations

- `OpenTelemetry.Instrumentation.EntityFrameworkCore` has no stable release; this project
  pins the latest beta (`1.17.0-beta.1`), which is expected for this instrumentation as of
  now.
- The EF Core spans are named `main` (the SQLite database name) rather than something more
  descriptive — that's the instrumentation library's default `db.name`-based span naming for
  SQLite, not something this task customizes.
