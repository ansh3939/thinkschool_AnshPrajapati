# Day 5 — Task 1: Diagnose a slow endpoint using traces

Copied `QuotesApi` from `day-4/task-7` (which already had OpenTelemetry tracing exported
to Jaeger, wired alongside JWT auth, EF Core, and Serilog) unchanged, then used its
existing tracing setup to diagnose and fix an intentionally slow endpoint. Nothing about
the OpenTelemetry/Jaeger configuration itself was touched — this task only exercises it.

## What changed vs. task-7

- `Program.cs`: `GET /api/quotes` temporarily got a `Thread.Sleep(1500)` added, then
  removed again once the trace confirmed it as the slow span. The endpoint is back to
  exactly what it was in task-7.
- `evidence/` — before/after trace exports pulled from Jaeger's API and the diagnosis
  note (see below).
- `QuotesApi.csproj`: `UserSecretsId` regenerated so this copy's local JWT signing key
  doesn't share a secrets store with `day-4/task-7`.

## Reproducing this

### 1. Start Jaeger (same image as day-4)

```bash
docker run -d --name quotesapi-jaeger \
  -p 16686:16686 -p 4317:4317 -p 4318:4318 \
  jaegertracing/all-in-one:latest
```

### 2. Set the local JWT signing key and run the API

```bash
cd QuotesApi
dotnet user-secrets set "Jwt:SigningKey" "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5501
```

### 3. Hit the endpoint a few times

```bash
curl http://localhost:5501/api/quotes
```

### 4. Open Jaeger

http://localhost:16686 → service `QuotesApi` → Find Traces → open a
`GET /api/quotes` trace.

## What was slow, and why

See [`evidence/DIAGNOSIS.md`](evidence/DIAGNOSIS.md) for the ~100-word diagnosis, and
[`evidence/README.md`](evidence/README.md) for the trace evidence (raw JSON exports from
Jaeger's API, plus what screenshots to take manually).

Short version: `GET /api/quotes`'s root span took ~1.57s while its only child span (the
EF Core query) took 630 microseconds — the time wasn't in the database, it was an
untraced `Thread.Sleep(1500)` blocking the handler. Removing it brought the root span
down to ~2.3ms, in line with the EF Core span.

## Bonus: KQL query for similarly slow endpoints

This project already carries the Azure Monitor OpenTelemetry exporter from
`day-4/task-6`/`task-7` (`UseAzureMonitor()` in `Program.cs`, gated on
`APPLICATIONINSIGHTS_CONNECTION_STRING` being set — it isn't, in this repo, so this
query is not something that was run against a live workspace here). If that connection
string were configured, this finds any endpoint with the same symptom — a slow request
whose own dependency calls don't explain the duration:

```kql
requests
| where timestamp > ago(1h)
| summarize avgDuration = avg(duration), requestCount = count() by name
| join kind=leftouter (
    dependencies
    | where timestamp > ago(1h)
    | summarize dependencyDuration = sum(duration) by operation_Name
  ) on $left.name == $right.operation_Name
| extend unexplainedDuration = avgDuration - coalesce(dependencyDuration, 0.0)
| where avgDuration > 500 and unexplainedDuration > 500
| project name, avgDuration, dependencyDuration, unexplainedDuration, requestCount
| order by unexplainedDuration desc
```

No Azure infrastructure was created for this task — the main exercise runs entirely
locally against Jaeger, per the task instructions.

## Verification

```bash
dotnet build QuotesApi.slnx
dotnet test QuotesApi.slnx
```

Build succeeded (0 warnings, 0 errors) and all 7 existing tests
(`JwtOptionsValidatorTests`, `JwtOptionsBindingTests`) still pass — unaffected by the
`GET /api/quotes` change since none of them cover that endpoint.

Manually verified: `POST /api/auth/login`, `GET /api/quotes` (before and after the fix),
`POST /api/quotes`, and `POST /api/quotes/import` all still work as in task-7.
