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

Versions come from git tags via MinVer (`v1.2.3`). Publishing a GitHub release triggers the `publish` job in
`.github/workflows/build.yml`, which pushes the validated packages to NuGet.org using the `NUGET_APIKEY` secret.
