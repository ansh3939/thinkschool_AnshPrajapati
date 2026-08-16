# Day 5 — Task 5: Verify in App Insights with your first KQL

`QuotesApi`, `azure.yaml`, and `infra/` are copied unchanged from `day-5/task-4`
(same quotes CRUD API, same `azd`-generated Container Apps + Application Insights
Bicep, reusing the existing Container Apps environment). `day-5/task-4` itself was
not touched.

## Why a code change was needed

`day-5/task-4/QuotesApi/Program.cs` has no OpenTelemetry setup — the repository-pattern
rewrite it copied from `day-5/task-2` dropped the OTel/Azure Monitor wiring that
`day-5/task-1` already has. Meanwhile `infra/resources.bicep` already provisions
Application Insights and injects `APPLICATIONINSIGHTS_CONNECTION_STRING` into the
container app's environment — the plumbing was there, the app just never read it.

So the only change in this folder's `QuotesApi/Program.cs` and `.csproj` is adding
back the same OpenTelemetry wiring `day-5/task-1` already uses (ASP.NET Core + EF Core
instrumentation, `UseAzureMonitor` gated on the connection string being present):

```csharp
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("QuotesApi"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    otel.UseAzureMonitor(options => options.ConnectionString = appInsightsConnectionString);
}
```

The connection string is never hardcoded — it only comes from `IConfiguration`,
populated in the deployed container by the env var `infra/resources.bicep` already
sets from the Application Insights resource it provisions. `azure.yaml` differs from
task-4's only in `name: task-5`, so `azd` doesn't collide with task-4's environment.

## Deploying

```bash
cd day-5/task-5
azd auth login
azd init -e thinkschool-task5   # reuses azure.yaml already in this folder
azd up
```

Prints the live app URL, e.g. `https://quotes-api.<hash>.centralindia.azurecontainerapps.io`.

If you already have a task-4 deployment and don't want a second set of billable
resources, copy `QuotesApi/Program.cs` and the OTel package references from this
folder's `.csproj` into your local task-4 checkout instead (without editing the repo's
`day-5/task-4/`), then `azd deploy` from there.

## Generating traffic

```bash
APP_URL=https://<your-app-url>

curl "$APP_URL/health"
curl "$APP_URL/api/quotes"
curl "$APP_URL/api/quotes" -X POST -H "Content-Type: application/json" \
  -d '{"author":"Ada Lovelace","text":"That brain of mine is something more than merely mortal."}'
curl "$APP_URL/api/quotes/1"
curl "$APP_URL/api/quotes?page=1&size=5"
```

Hit a few endpoints a few times each — the KQL groups by endpoint name, so more
variety makes the result more interesting. Telemetry can take a minute or two to
show up in Application Insights.

## Opening Application Insights

```bash
azd show
```

Click "View in Azure Portal", open the resource group, open the Application Insights
resource inside it, then go to **Logs** in the left nav.

## The KQL query

```kusto
requests
| where timestamp > ago(30m)
| summarize count(), p50=percentile(duration, 50), p99=percentile(duration, 99) by name
| order by p99 desc
```

- `requests` — one row per incoming HTTP request, written by the ASP.NET Core
  OpenTelemetry instrumentation via `UseAzureMonitor()`.
- `p50` / `p99` — median and 99th-percentile request duration in ms, per endpoint
  (`name`). A p99 much higher than p50 flags occasional slow outliers.
- `order by p99 desc` — worst tail latency first.

### Saving it as a function

In the Logs blade: run the query once, click **Save** → **Save as function**, give it
a name (e.g. `RequestLatencyByEndpoint`) and alias, save. Re-run it later from
**Logs** → **Functions**, or reference it from another query.

## What requires manual Azure work

Everything below happens in Azure, not in this repo — nothing here is fabricated:

1. Deploy (`azd up`, or reuse an existing deployment as above).
2. Hit the endpoints above against the live app URL.
3. Open Application Insights (`azd show` → Portal → Logs).
4. Run the KQL query against real telemetry.
5. Save it as a function.
6. Take the screenshot and write the observation — see `evidence/README.md`.

## Verification actually performed

- `dotnet build QuotesApi` — succeeded, 0 warnings, 0 errors.
- Confirmed `day-5/task-4/QuotesApi/Program.cs` has no OpenTelemetry code before
  adding it here.
- Confirmed `infra/resources.bicep` already sets `APPLICATIONINSIGHTS_CONNECTION_STRING`
  from the `monitoring` module — no infra change needed.
- `azd up` was **not** run — no live deployment exists for this task yet, so no
  telemetry, screenshot, or KQL result is claimed here. See `evidence/README.md`.
