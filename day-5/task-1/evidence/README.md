# Evidence

`before-trace.json` and `after-trace.json` are raw exports pulled directly from
Jaeger's HTTP API (`GET /api/traces?service=QuotesApi`) while reproducing this
exercise, so the numbers in `DIAGNOSIS.md` are real, not estimated.

- **Before** (`934ffe4e6f8ad843a46c3d512a59c0e5`): root `GET /api/quotes` span =
  1,573,004 µs (~1.57s). Child EF Core `main` span = 630 µs.
- **After** (`470426934cbbfd02ef4cba333bb9744c`): root `GET /api/quotes` span =
  2,305 µs (~2.3ms). Child EF Core `main` span = 485 µs.

## Screenshots still needed (manual)

I can't capture screenshots of your local Jaeger UI directly, so please take
these yourself and save them in this folder:

1. **`before-screenshot.png`** — with the app still on the `Thread.Sleep(1500)`
   version (or after re-adding it temporarily), open
   http://localhost:16686, select service `QuotesApi`, click "Find Traces",
   open the trace for `GET /api/quotes` with a duration around 1.5s, and
   screenshot the trace timeline. It should show the root `GET /api/quotes`
   span taking ~1.5s with a tiny `main` (EF Core) child span nested inside,
   leaving a large gap of unexplained time in the root span.
2. **`after-screenshot.png`** — with the fix in place (current code, no
   `Thread.Sleep`), hit `GET /api/quotes` again, find the new trace in
   Jaeger, and screenshot it. The root span should now be a few milliseconds,
   about the same size as the `main` child span.

## How to reproduce locally

```bash
# 1. Start Jaeger (same container/image as day-4)
docker run -d --name quotesapi-jaeger \
  -p 16686:16686 -p 4317:4317 -p 4318:4318 \
  jaegertracing/all-in-one:latest
# (or `docker start quotesapi-jaeger` if it already exists)

# 2. Run the API
cd day-5/task-1/QuotesApi
dotnet user-secrets set "Jwt:SigningKey" "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5501

# 3. Hit the endpoint a few times
curl http://localhost:5501/api/quotes

# 4. Open Jaeger UI
open http://localhost:16686
```
