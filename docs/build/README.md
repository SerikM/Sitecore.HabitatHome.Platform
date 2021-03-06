# Deployment Options

## Deploying Locally With Docker

### Docker Deployment Known Issues

- Need to manually run "Populate Solr Managed Schemas" in the Control Panel before Solr indexing works

### Docker Deployment Prerequisites

- Sitecore base images built (either locally or in a registry)
  - see [Docker-Images Repo](https://github.com/sitecore/docker-images) for more details
- Windows version 1809 or later
- Docker (Engine) for Windows version 19.03 or later
- "az" PowerShell module
  - [https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-windows?view=azure-cli-latest](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-windows?view=azure-cli-latest)

### Starting Up Your Sitecore Instance

- Ensure your Sitecore license is in `c:\license`
- Modify .env file:
  - `REGISTRY`: set your registry url (trailing slash (/) is important).
  - `REMOTEDEBUGGER_PATH`: Ensure the path is valid. You might have to change `Enterprise` to `Professional` or `Community`.
- Set the SA password to the one configured in your base image
- login to your docker registry
  - for Azure ACR:
    - `az login`
    - `az acr login --name <registryname>`
- run `docker-compose up -d`
  - this will pull all of the necessary base images and spin up your Sitecore environment. It will take **quite some time** if this is the first time you execute it.

### Deploying to Docker

Confirm that you can access the Sitecore instance deployed using docker-compose in the previous step by browsing to [http://127.0.0.1:44001/sitecore](http://127.0.0.1:44001/sitecore) which is the default endpoint for the CM role specified in the docker-compose.yml file. Ensure you replace it with your own value if you changed it!

1. Review the `cake-config.json` file if you've made any changes to the endpoints or if you need to change the default settings.
1. Smart publish the site.
1. Run `.\build.ps1 -DeploymentTarget Docker`
1. Run Docker post-deployment steps below.

### Docker Post-Deployment Steps

1. Open the Content Editor
1. Smart publish the site in all languages

### Cleaning and Re-deploying With Docker

In case you want to start over.

1. Run `docker-compose down`
2. Run `.\CleanDockerData.ps1`
3. At this point you can start again with `docker-compose up -d` to have a fresh installation of Sitecore with no files/items deployed!

### Troubleshooting Docker Deployment

#### unauthorized: authentication required

When running `docker-compose up -d`, you get the following error:

```text
ERROR: Get https://<registryname>.azurecr.io/v2/<someimage>/manifests/<someimage>: unauthorized: authentication required
```

This indicates you are not logged in your registry. Run `az acr login --name <registryname>` and retry.

## Deploying to Local IIS Site

Requires a local working instance of Sitecore Experience Platform which matches the version of the demo you're trying to deploy along with the relevant version of Sitecore Experience Accelerator (SXA).

1. Confirm that you can access the Sitecore instance by browsing to [https://habitathome.dev.local/sitecore](https://habitathome.dev.local/sitecore) which is the default hostname when installing using the [Habitat Home Utilies](https://github.com/sitecore/sitecore.habitathome.utilities) repository. Ensure you replace it with your own value if you changed it!

1. Run `.\build.ps1`

## Packaging Site Assets

This functionality allows you to publish Web, xConnect and items (in the form of DACPACs) to the local Publish folders.

The process involves compiling and publishing code assets, gathering yml item files and converting them to DACPACs using Sitecore.Courier and Sitecore Azure Toolkit.

### Packaging Site Assets Prerequisites

1. Sitecore Azure Toolkit (assumed to be located at c:\sat - change in `cake-config.json` if different) [Sitecore Azure Toolkit on dev.sitecore.com](https://dev.sitecore.net/~/media/0804C3F4CC524149B32AD25D52CDCA12.ashx)

### Packaging Site

1. Run `.\build.ps1 -DeploymentTarget Local`

Published assets will be found in `.\Publish` folder in the `Web`, `xConnect` and `Data` folders.
