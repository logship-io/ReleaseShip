# ReleaseShip

ReleaseShip is an ASP.NET Core application for hosting release artifacts. It now supports:

- **binary artifact storage** for the existing package/release workflows
- **OCI-style container registry flows** for blobs, manifests, and tags
- **a small React admin UI** served by the same application at `/admin/`

## Repository layout

| Path | Purpose |
| --- | --- |
| `src\ConsoleHost` | ASP.NET Core host, controllers, auth, and static file hosting |
| `src\Data` | shared data/storage services and SQLite-backed metadata |
| `src\WebAdmin` | React/Vite admin UI source |
| `build\container\Containerfile` | multi-stage container image build |
| `tests\ReleaseShip.ConsoleHost.Tests` | integration and unit tests |

## Prerequisites

- .NET 8 SDK
- Node.js 20+ and npm

## Local development

### Backend

Build and test the full repo:

```powershell
dotnet build .\dirs.proj
dotnet test .\dirs.proj
```

Run the application:

```powershell
dotnet run --project .\src\ConsoleHost\ReleaseShip.ConsoleHost.csproj
```

By default in development:

- the app listens using `launchSettings.json`
- SQLite metadata is stored in `src\ConsoleHost\release-ship.db`
- local file-backed content uses `RootDirectory`
- the bootstrap admin user comes from `appsettings.Development.json`

### Frontend

Install dependencies:

```powershell
Set-Location .\src\WebAdmin
npm install
```

Run the frontend dev server:

```powershell
npm run dev
```

Build the frontend bundle for the ASP.NET host:

```powershell
npm run build
```

The React build writes static assets into:

```text
src\ConsoleHost\wwwroot\admin
```

The ASP.NET host serves that bundle from:

```text
/admin/
```

## Container image build

Build the application image:

```powershell
docker build -f .\build\container\Containerfile -t releaseship:local .
```

The container build now compiles the React admin UI automatically:

1. `npm ci` runs in `src\WebAdmin`
2. `npm run build` emits static files into `src\ConsoleHost\wwwroot\admin`
3. `dotnet publish` packages the host and the generated UI into the final image

## Configuration

Primary configuration lives in:

- `src\ConsoleHost\appsettings.json`
- `src\ConsoleHost\appsettings.Development.json`

### Core settings

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:Default` | SQLite connection string for metadata |
| `RootDirectory` | local filesystem root for file-backed content and default container blob storage |
| `AllowedHosts` | standard ASP.NET Core host binding setting |

### Authentication

| Key | Purpose |
| --- | --- |
| `Authentication:BootstrapAdmin:Username` | bootstrap admin username created at startup |
| `Authentication:BootstrapAdmin:Password` | bootstrap admin password created at startup |

The bootstrap admin can:

- access authenticated admin APIs
- log into the React admin UI
- issue scoped basic-auth tokens for registry operations

### Storage

| Key | Purpose |
| --- | --- |
| `Storage:Containers:RootDirectory` | optional override for container blob/upload storage root |
| `Storage:S3:bucket` | S3 bucket for binary artifact storage |
| `Storage:S3:serviceEndpoint` | S3-compatible endpoint |
| `Storage:S3:accessKey` | S3 access key |
| `Storage:S3:secretKey` | S3 secret key |

Notes:

- binary artifact storage already uses the S3 settings above
- container storage currently defaults to the local filesystem
- container storage was designed with pluggable providers in mind, but the current implementation is filesystem-backed

## Registry and UI behavior

- public repositories can allow anonymous container pulls
- push/delete flows require authenticated access
- protected tag patterns can block tag overwrite
- admin APIs and admin UI actions are behind basic-auth login
- the public UI surfaces namespaces, public container repositories, and existing binary package listings

## Current validation

Automated validation currently covers:

- registry ping, catalog, upload, manifest, and tag flows
- protected-tag enforcement
- auth-gated admin and write operations
- filesystem-backed blob storage behavior

The remaining manual acceptance step is live client verification with Docker and Podman in an environment where those runtimes are available.
