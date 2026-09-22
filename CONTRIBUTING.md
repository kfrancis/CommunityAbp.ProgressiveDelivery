# Contributing

Thanks for helping improve CommunityAbp.ProgressiveDelivery.

## Prerequisites

- .NET SDK 10.0.100 or later (see `global.json`)
- No database is required: tests run against in-memory SQLite.

## Build and test

```bash
dotnet build
dotnet test
```

`dotnet test` uses the Microsoft.Testing.Platform runner (configured in `global.json`). Do not pass VSTest-only
arguments such as `-nologo`; they are forwarded to the test application and rejected. To run one project:

```bash
dotnet test --project test/CommunityAbp.ProgressiveDelivery.Domain.Tests
```

## Layout

- `src/` – one project per package (ABP module layering).
- `test/` – TUnit + Shouldly + NSubstitute. `TestBase` holds shared seed data; `EntityFrameworkCore.Tests` hosts the
  SQLite module that Domain, Application and AspNetCore tests build on.
- `docs/adr/` – architecture decision records. Add one for any non-obvious design change.

## Conventions

- Central package management (`Directory.Packages.props`); keep ABP packages on the same `$(AbpVersion)`.
- Nullable and warnings-as-errors are on. Fix the warning, do not suppress it, unless there is a documented reason.
- Object mapping uses Mapperly through `Volo.Abp.Mapperly`; no AutoMapper.
- Never add a per-execution telemetry table. Execution data belongs in OpenTelemetry.
- Keep the core free of vendor SDKs (Sentry, Intercom, ...). Integrations implement `IProgressiveDeliveryTelemetry` in
  their own package.
- Every behaviour change ships with a test in the matching test project.

## Releases

Versions come from git tags via MinVer (`v1.2.3`, pre-releases such as `v1.2.3-preview.1`). Between tags, builds
get `last-tag + height` with the `preview.0` identifier, so local builds never collide with a released version.

Publishing a GitHub release triggers the `publish` job in `.github/workflows/build.yml`, which pushes the validated
packages to NuGet.org via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing). No
API key is stored anywhere; the job's `id-token: write` permission and `NuGet/login@v1` exchange the GitHub OIDC
token for a short-lived key at run time.

One-time setup (repo owner):

1. nuget.org -> profile -> Trusted Publishing -> Add policy: repository owner `kfrancis`, repository
   `CommunityAbp.ProgressiveDelivery`, workflow file `build.yml`, environment empty. A new policy must be used within
   7 days to become permanently active.
2. GitHub repo -> Settings -> Secrets and variables -> Actions -> Variables: `NUGET_USER` = the nuget.org profile
   name that owns the policy.

Cutting a release:

```powershell
git tag v0.1.0-preview.1
git push origin v0.1.0-preview.1
```

Then create a GitHub release from that tag. The `build` workflow runs tests, packs, validates and publishes.
