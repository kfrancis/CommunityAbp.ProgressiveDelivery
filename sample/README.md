# Sample host (Aspire)

A click-through ABP MVC host for the Progressive Delivery UI, orchestrated by .NET Aspire so the
`progressive_delivery.*` traces and metrics show up in the Aspire dashboard.

```
sample/
  CommunityAbp.ProgressiveDelivery.Sample.AppHost          Aspire AppHost (dashboard + orchestration)
  CommunityAbp.ProgressiveDelivery.Sample.ServiceDefaults  OpenTelemetry / health checks / resilience defaults
  CommunityAbp.ProgressiveDelivery.Sample.Web              ABP MVC host: Basic theme, SQLite, dev auth, seeded data
```

## Run

Prerequisites: .NET 10 SDK, Node.js + Yarn (for `abp install-libs`), ABP CLI (`dotnet tool install -g Volo.Abp.Studio.Cli`).

```bash
dotnet run --project sample/CommunityAbp.ProgressiveDelivery.Sample.AppHost --launch-profile http
```

- Aspire dashboard: http://localhost:15272 (anonymous access enabled for the sample)
- Web host: http://localhost:5170 (Home → Feature tracks / Subject inspection / Transition history / Execution playground / Swagger)

The first build runs `abp install-libs` to pull the client-side libraries into `wwwroot/libs` (git-ignored).
The SQLite file `progressive-delivery-sample.db` is created next to the Web project and seeded on first start;
delete it to reset the demo.

## What is in the box

- **No Identity.** `DevAuthenticationHandler` signs every request in as a sample user. The `pd-role` cookie
  (switch on the home page) selects **ops** (every `ProgressiveDelivery.*` permission) or **support** (view-only),
  which is how you see permission gating in the UI. `SampleRolePermissionValueProvider` is the only permission
  value provider because ABP's `NullPermissionStore` answers *Prohibited* for batch checks and would veto it.
- **Seeded tracks** from the design mockups: `Cab.Patients.Search` (5 levels, official 1, rollouts on 2 and 3),
  `Cab.Patients.Duplicates`, `Cab.Billing.Statements`, `Cab.Documents.Ocr`, `Cab.Portal.Upload` (disabled),
  `Cab.Scheduling.Slots`; plus assignments and history for the two sample users, a client and a tenant.
- **Execution playground** (`/Demo`): runs `IProgressiveDelivery.ExecuteAsync` with routes you can make throw,
  shows before/after resolution and the telemetry events, so you can watch demotion and fallback happen.
- **Ports** 5170/5171 avoid Windows' Hyper-V reserved ranges (44300–44399 are often excluded).

Never reuse the dev authentication or permission provider outside this sample.
