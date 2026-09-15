# Claude Design prompt: Progressive Delivery administration UI

Paste the block below into Claude Design. It describes the administrator-facing UI for this module. The data model,
permissions and API shapes referenced are the ones implemented in this repository.

---

Design an administration and support UI for **Progressive Delivery**, an ABP Framework module that moves users,
tenants and client applications through *versioned implementations* of a behaviour. This is an operations tool used
by engineers and support staff inside an existing ABP admin application (LeptonX-style shell: left navigation, top
bar, content area). Design it as a set of screens on one canvas, desktop-first, with a light and a dark variant.

## Core concepts the UI must make obvious

- A **Feature Track** (e.g. `Cab.Claims.Loading`) is an independently versioned behaviour. Tracks never share state.
- Each track has cumulative **integer levels** starting at 0 ("original implementation"). Level 3 includes levels
  0, 1 and 2. Levels are a line, not a set of switches — never render them as toggles.
- **Official level** is the minimum everyone gets. **Highest available level** is the top of the line. Levels above
  official are **experimental**.
- A **Rollout** targets one level with a percentage (shown as % with two decimals; stored as basis points). Growth is
  sticky: a subject included at 5 % stays included at 25 %.
- An **Assignment** pins a subject (`User`, `Tenant`, `Client`, `Anonymous`, custom) to a level. Assignments below
  official are harmless and resolve to official; do not show them as errors, show them as "resolves to official".
- **Effective level** = max(official, assigned/cohort) capped by constraints (client capability, emergency cap).
- A **Transition** is a durable history record: Promotion, AutomaticPromotion, ManualOverride, AutomaticDemotion,
  OfficialLevelChanged, AdministrativeReset, EmergencyOverride. Each carries reason, correlation id, trace id, tenant.
- Every level has a **Fallback policy**: `None`, `DemoteOnly`, `SafeRead`, `Idempotent`. Only the last two allow
  automatic retry of a lower level after failure. Communicate that `None` is the safe default for writes.

## Screens

1. **Tracks overview** — table of tracks: name, display name, official / highest level, enabled state, active
   rollouts summary (e.g. "L2 → 25 %"), demotions in the last 24 h (from telemetry, may be "n/a"), last transition.
   Row click opens the track. Primary action: "New track". Filter by name.

2. **Track detail** — the hero of the product. Show the level line horizontally: numbered level pills from 0 to
   highest, the official level clearly marked (e.g. solid pill + "official" label), experimental levels visually
   lighter, rollout percentage badges under levels that have one, and a "promote to official" affordance that opens a
   confirmation explaining "assignments are not modified; anything below N resolves to N". Below the line, tabs:
   - *Levels*: level number, description, support description (multi-line, for support staff), performance-sensitive
     flag, fallback policy (radio/segmented with short safety explanation). Add level appends only (numbers are
     immutable).
   - *Rollouts*: one card per rollout: target level, percentage slider with preset stops (1, 5, 10, 25, 50, 100 %),
     status (Active / Paused / Suspended / Completed), "allow skipping intermediate levels" checkbox with a warning,
     pause/resume/suspend/remove actions. Explain that shrinking does not remove already-assigned subjects.
   - *Assignments*: paged table filtered by subject type/id, showing assigned level, reason, tenant, last change; row
     actions "override" and "reset".
   - *History*: transition timeline for the track (official changes and subject transitions), each entry showing type,
     from → to, subject, reason, correlation/trace ids as copyable chips.
   - *Danger zone*: disable track (everyone drops to official), delete track.

3. **Subject inspection (support view)** — search box: subject type + subject id (+ tenant for host users). Result:
   for the chosen track (or all tracks), a card with official / assigned / cohort / effective level, an
   "experimental" badge, the list of **differences** — every level between official+1 and effective with its support
   description — and any **constraints** that lowered the level (source such as `client-capability` or
   `options-cap`, with reason). Actions available only with the override permission: "Override level…" (with
   reason, emergency checkbox) and "Reset to official". This screen must be usable by non-engineers.

4. **Override dialog** — track, subject, target level picker rendered on the same level line, reason (required),
   "emergency override" checkbox (changes the recorded transition type and styling), summary of what the subject will
   run after the change.

5. **Transition history (global)** — filterable list across tracks: track, type, subject, from → to, reason, when,
   correlation/trace. Emphasise `AutomaticDemotion` entries (they are incidents); allow "open trace" as an external link
   placeholder.

## Permissions (drive what is visible / enabled)

`ProgressiveDelivery.Tracks`, `.Tracks.Manage`, `.Levels`, `.Levels.Manage`, `.Assignments`, `.Assignments.View`,
`.Assignments.Override`, `.Rollouts`, `.Rollouts.Manage`, `.Telemetry`. Support staff typically have
`Assignments.View` only: they can inspect but every mutating control must be hidden or disabled with a tooltip.

## Tone and constraints

- Operational, calm, dense but legible. Status colours: official = primary, experimental = accent, demotion/incident =
  warning, emergency override = danger. Never use red for "experimental".
- Multi-tenant: show the tenant context in the top bar; host users can pick a tenant when inspecting.
- All destructive or state-changing actions confirm with a one-sentence consequence statement.
- Empty states should teach the model ("Add a level to start a rollout").
- Provide a compact mobile layout for the inspection screen only (support on the go).
