# Day 5 — Task 3: Azure Container Apps Fundamentals

For this task, I created a resource group and then created a Container Apps environment inside it using Azure CLI.

## Azure CLI commands

First, I created the resource group:

```bash
az group create -n thinkschool-rg -l centralindia
```

Then I created the Container Apps environment:

```bash
az containerapp env create -n thinkschool-env -g thinkschool-rg -l centralindia
```

I got an error because the subscription did not have the `Microsoft.OperationalInsights` provider registered. I registered it and then ran the environment creation command again:

```bash
az provider register -n Microsoft.OperationalInsights --wait
```

After that, I checked the environment:

```bash
az containerapp env show -n thinkschool-env -g thinkschool-rg
```

## What I understood from the commands

* `az group create` creates the resource group that holds the Azure resources.
* `az containerapp env create` creates the environment where Container Apps can be deployed.
* `az containerapp env show` lets me check the environment and see its current configuration and status.

The environment also has Log Analytics configured for logs. Azure created the required setup automatically because I did not provide a Log Analytics workspace myself.

## Environment output

This is the output I got from my Azure subscription on 2026-08-15:

```json
{
  "id": "/subscriptions/fac1dffd-0fa8-4afe-a025-5b542e7aa204/resourceGroups/thinkschool-rg/providers/Microsoft.App/managedEnvironments/thinkschool-env",
  "location": "Central India",
  "name": "thinkschool-env",
  "properties": {
    "appInsightsConfiguration": null,
    "appLogsConfiguration": {
      "destination": "log-analytics",
      "logAnalyticsConfiguration": {
        "customerId": "4dff97bf-ccca-4e65-9810-4b9f15cbac8f",
        "sharedKey": null
      }
    },
    "customDomainConfiguration": {
      "certificateKeyVaultProperties": null,
      "certificatePassword": null,
      "certificateValue": null,
      "customDomainVerificationId": "643C79666260C0C652CC21EBF7DA7016A62ADFE5003E1465A50FD8D655127572",
      "dnsSuffix": null,
      "expirationDate": null,
      "subjectName": null,
      "thumbprint": null
    },
    "daprAIConnectionString": null,
    "daprAIInstrumentationKey": null,
    "daprConfiguration": {
      "version": "1.16.4-msft.11"
    },
    "defaultDomain": "purpleriver-6a51be2f.centralindia.azurecontainerapps.io",
    "eventStreamEndpoint": "https://centralindia.azurecontainerapps.dev/subscriptions/fac1dffd-0fa8-4afe-a025-5b542e7aa204/resourceGroups/thinkschool-rg/managedEnvironments/thinkschool-env/eventstream",
    "infrastructureResourceGroup": null,
    "ingressConfiguration": null,
    "kedaConfiguration": {
      "version": "2.18.1"
    },
    "openTelemetryConfiguration": null,
    "peerAuthentication": {
      "mtls": {
        "enabled": false
      }
    },
    "peerTrafficConfiguration": {
      "encryption": {
        "enabled": false
      }
    },
    "provisioningState": "Succeeded",
    "publicNetworkAccess": "Enabled",
    "staticIp": "135.235.248.29",
    "vnetConfiguration": null,
    "workloadProfiles": [
      {
        "enableFips": false,
        "name": "Consumption",
        "workloadProfileType": "Consumption"
      }
    ],
    "zoneRedundant": false
  },
  "resourceGroup": "thinkschool-rg",
  "systemData": {
    "createdAt": "2026-08-14T21:03:13.985259",
    "createdBy": "ansh.praja549@gmail.com",
    "createdByType": "User",
    "lastModifiedAt": "2026-08-14T21:03:13.985259",
    "lastModifiedBy": "ansh.praja549@gmail.com",
    "lastModifiedByType": "User"
  },
  "type": "Microsoft.App/managedEnvironments"
}
```

The environment was created successfully because the provisioning state is:

```text
"provisioningState": "Succeeded"
```

## What I learned

The main thing I learned from this task is that a Container Apps environment is set up before deploying the actual container app.

I also got to see how Azure CLI can be used to create and check Azure resources instead of doing everything through the Azure Portal.

The environment can later be used for things like:

* deploying a container image
* configuring ingress and target ports
* creating revisions
* setting up autoscaling

I did not deploy a container app in this task. I only created the resource group and the Container Apps environment.
