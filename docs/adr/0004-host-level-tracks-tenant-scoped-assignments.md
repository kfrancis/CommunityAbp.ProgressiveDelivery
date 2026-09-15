# ADR 0004: Tracks are host-level; assignments and transitions are tenant-scoped

## Status
Accepted

## Context
Tracks describe code that is deployed once for all tenants. Which subject runs which level is per tenant. Cache keys
must never leak assignment data across tenants.

## Decision
- `FeatureTrack`, `FeatureLevel`, `FeatureRollout`: not `IMultiTenant`; cache items are `[IgnoreMultiTenancy]`.
- `FeatureAssignment`, `FeatureTransition`: `IMultiTenant`. `FeatureSubject` carries `TenantId`; every read/write of
  assignment data runs inside `CurrentTenant.Change(subject.TenantId)`, so the ABP data filter and tenant-prefixed
  cache keys apply consistently even when the host acts on behalf of a tenant.
- Cache invalidation captures the tenant at scheduling time and re-establishes it in the post-commit callback,
  because ABP normalises cache keys (tenant prefix) when the removal runs, not when it is scheduled.
- Management APIs confine tenant callers to their own tenant; only the host may target another tenant explicitly.

## Consequences
- Tenant-scoped rollouts and tenant-wide maximum levels are not entities yet; use constraint providers (roadmap).
