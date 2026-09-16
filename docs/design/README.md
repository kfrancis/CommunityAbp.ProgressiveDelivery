# Admin UI design

Source of truth for the administrator / support UI. Produced with Claude Design from
[admin-ui-design-prompt.md](admin-ui-design-prompt.md); implementation must follow it, not the other way round.

| File | What it is |
|------|------------|
| `Progressive Delivery Admin.dc.html` | Design canvas source (Claude Design `.dc.html`). One templated page; the `screen` state selects the artboard. Keep the file name — Claude Design round-trips on it. |
| `support.js` | Generated Claude Design runtime the canvas needs to render (`dc-runtime`). Do not edit. |
| `admin-ui-design-prompt.md` | Brief the canvas was generated from. |

Open in Claude Design: project *Progressive Delivery Admin* (share link in the original conversation), or upload the
two files above to a new project. Do not hand-edit the `.dc.html`; edit in Claude Design and re-download.

## Screens (`state.screen`)

| Screen | Role | Purpose |
|--------|------|---------|
| `tracks` | Operations | Track overview: name, official / highest level, enabled, active rollouts, demotions (24 h), last transition. KPI tiles: tracks, active rollouts, demotions 24 h, emergency overrides 7 d. |
| `track` | Operations | Track detail. Horizontal **level line** (official = solid primary, experimental = dashed accent, capped = muted). Tabs: Levels (fallback policy segmented control with safety note), Rollouts (percentage with preset stops, pause/suspend/remove), Assignments, History, Danger zone. |
| `inspect` | Operations | Subject inspection: official / assigned / cohort / effective, **differences** list (every level in `(official, effective]` with support description), constraints with source. Actions: Override…, Reset. |
| `history` | Operations | Global transition history; `AutomaticDemotion` and `EmergencyOverride` emphasised as incidents; correlation / trace ids as copyable chips. |
| `support-track` | Support (`Assignments.View` only) | Track detail with every mutating control disabled + tooltip "Requires ProgressiveDelivery.<permission>". |
| `support-inspect` | Support | Inspection without override/reset. |
| `mobile` | Support | Compact inspection layout. |

Modals (`state.modal`): `promote`, `disable`, `del`, `suspend`, `remove`, `reset`, `newtrack`, `addlevel`,
`newrollout`, plus the override dialog. Each states one consequence sentence and what transition gets recorded.

## Visual language

- Fonts: IBM Plex Sans / IBM Plex Mono (track names, ids, percentages in mono).
- Tokens (see `[data-pd="light|dark"]` in the file): `--pri` official/primary (teal), `--acc` experimental (violet),
  `--warn` demotion/incident, `--dgr` emergency / irreversible. Experimental is never red.
- Percentages displayed as `25.00 %`, stored as basis points (`2500`).
- Tenant context in the top bar; host users can switch tenant when inspecting.

## Implementation plan (ABP UI module conventions)

The UI ships as ABP UI modules, not as a standalone app:

- `CommunityAbp.ProgressiveDelivery.Web` — MVC / Razor Pages module (`ProgressiveDeliveryWebModule`, depends on
  `AbpAspNetCoreMvcUiThemeSharedModule` + `Application.Contracts`). Pages under `Pages/ProgressiveDelivery/...`
  embedded via `AbpVirtualFileSystemOptions`, a `ProgressiveDeliveryMenuContributor` (`IMenuContributor`) adding the
  *Progressive Delivery* menu group, `[Authorize(ProgressiveDeliveryPermissions.*)]` on every page model, bundles via
  `AbpBundlingOptions`, localisation from `ProgressiveDeliveryResource`, `AbpNavigationOptions` + toolbar unchanged.
  Theme-agnostic: Basic and LeptonX both render it; styling via theme CSS variables, not hard-coded colours.
- `CommunityAbp.ProgressiveDelivery.Blazor` (+ `.Blazor.Server` / `.Blazor.WebAssembly` per ABP layout) — same pages
  as Blazorise components, `ProgressiveDeliveryMenuContributor` for `MenuNames.Application.Main`, HTTP calls through
  the existing `IFeatureTrackAppService` / `IProgressiveDeliveryInspectionAppService` proxies.
- Nothing in the UI packages talks to repositories; they consume the application contracts only, so tiered
  deployments work through `HttpApi.Client`.
- Permissions drive visibility exactly as in the `support-*` screens: hide or disable with a tooltip, never 403 after
  the click.
