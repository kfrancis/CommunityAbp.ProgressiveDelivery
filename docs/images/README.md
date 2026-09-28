# Documentation images

Source: `Docs Images.dc.html` in the design project. Every image has a light and a dark variant at 2× resolution.
Copy `docs/images/` into the repo and embed with `<picture>` so GitHub picks the variant that matches the reader's theme.

| File | Use in README section |
|------|------------------------|
| `hero-*.png` (1280×640) | Top of README; also the GitHub social preview (use `hero-light.png`) |
| `levels-vs-flags-*.png` | Why integer levels instead of boolean feature flags? |
| `independent-tracks-*.png` | Feature tracks |
| `effective-level-resolution-*.png` | Effective-level resolution |
| `sticky-rollout-*.png` | Sticky rollout, ADR 0005 |
| `automatic-demotion-*.png` | Automatic demotion / fallback, ADR 0003 |
| `fallback-policies-*.png` (1200×560) | Safe reads vs writes |
| `architecture-*.png` | Architecture |

## Snippet

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/effective-level-resolution-dark.png">
  <img alt="Effective-level resolution: official floor, assignment, rollout cohort, constraints, effective level" src="docs/images/effective-level-resolution-light.png" width="800">
</picture>
```

Keep the existing ASCII diagrams and tables beneath each image. They stay searchable and diffable, and they render in NuGet's README view, which ignores `<picture>`.
