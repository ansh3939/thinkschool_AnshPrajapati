# Day 5 — Task 4: Deploy via azd CLI

`QuotesApi` copied unchanged from `day-5/task-2` — the same container-ready quotes
CRUD API (SQLite + EF Core, `.csproj`-based container publishing, `/health` endpoint).
No Dockerfile, no app code changes here; this task only adds the `azd`
orchestration and Bicep infra on top of it.

## Install

```bash
brew install azd
```

## What was run

```bash
cd day-5/task-4
azd init --from-code -e thinkschool-task4 -l centralindia -s <subscription-id>
```

Picked "Use code in current directory" → "Confirm and continue initializing my app".
`azd` detected the `.NET` project in `QuotesApi/` and generated `azure.yaml`.

By default `azd` synthesizes the Bicep in memory at deploy time. To get `infra/main.bicep`
and `infra/main.parameters.json` on disk (as this exercise asks for), ran:

```bash
azd infra gen -e thinkschool-task4 --force
```

This produced:

```
infra/
  main.bicep              # subscription-scope entry point: creates the resource group
  resources.bicep         # the actual resources, deployed into that group
  main.parameters.json    # maps azd environment vars into the bicep parameters
  abbreviations.json       # Azure resource-type naming abbreviations used by resources.bicep
  modules/fetch-container-image.bicep  # looks up the current image tag on redeploys
```

## `azure.yaml`

```yaml
name: task-4
metadata:
    template: azd-init@1.31.1
services:
    quotes-api:
        project: QuotesApi
        host: containerapp
        language: dotnet
resources:
    quotes-api:
        type: host.containerapp
        port: 8080
```

`services.quotes-api.project` points at the `QuotesApi/` folder copied in from task-2 —
`azd` builds that project's container image (via the same `dotnet publish
/t:PublishContainer` mechanism as task-2, just driven by `azd` instead of run by hand)
and deploys it as the `quotes-api` container app on port 8080, matching the port
`QuotesApi` listens on inside its container.

## What `infra/resources.bicep` provisions

- **Log Analytics + Application Insights** (`br/public:avm/ptn/azd/monitoring`) — required
  by the Container Apps environment for logs, same as the manual `az containerapp env create`
  in `day-5/task-3` (which auto-created a Log Analytics workspace for the same reason).
- **Azure Container Registry** (`br/public:avm/res/container-registry/registry`) — where
  `azd` pushes the built image.
- **Container Apps environment** (`br/public:avm/res/app/managed-environment`) — the
  same kind of environment created manually in `day-5/task-3`, just provisioned here
  as part of `azd up` instead of a standalone `az containerapp env create`.
- **User-assigned managed identity** — lets the container app pull from ACR without a
  password/secret.
- **The `quotes-api` container app** (`br/public:avm/res/app/container-app`) — ingress on
  port 8080, 1–10 replicas, pulling the image `azd` built and pushed.

This is the standard output `azd infra gen` produces for a plain `.NET` web project —
nothing added or trimmed beyond what the tool generated.

## Deploying

```bash
azd up
```

Builds `QuotesApi`'s container image, pushes it to the generated ACR, provisions the
Bicep above, and deploys the image as a Container Apps revision. Prints the live URL
in the form:

```
https://quotes-api.<hash>.centralindia.azurecontainerapps.io
```

## Verifying

Same curls that worked locally in task-2:

```bash
curl https://<app-url>/health
# {"status":"healthy"}

curl https://<app-url>/api/quotes
# {"page":1,"size":10,"total":0,"items":[]}
```

## Status

`azd init` and `azd infra gen` were run for real against this subscription (output
above is real, not fabricated) and the Bicep was validated with `az bicep build`
(compiles cleanly — only two harmless "unused parameter" linter warnings on
`principalId`/`principalType`, which `azd`'s generated `resources.bicep` always
declares for RBAC even when a given resource module doesn't consume them).

`azd up` itself was **not** run — provisioning real billable Azure resources
(new resource group, ACR, Container Apps environment, Log Analytics, Application
Insights) was left as a manual step rather than executed automatically. To finish
this task, run from `day-5/task-4`:

```bash
az login
azd up
```

`azd` will prompt for the Azure subscription and location (or reuse the
`thinkschool-task4` environment already recorded under `.azure/`, which points at
`centralindia`) then print the live URL once deployment finishes.

## Verification actually performed

- `dotnet build QuotesApi` — succeeded, 0 warnings, 0 errors (unchanged app code from
  task-2).
- `azd init --from-code` — succeeded, generated `azure.yaml`.
- `azd infra gen --force` — succeeded, generated `infra/`.
- `az bicep build --file infra/main.bicep` — compiles with no errors.
- `azd up` — not run; no Azure Container Apps URL exists yet for this task, and none
  is claimed here.
