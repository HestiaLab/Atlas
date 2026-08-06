# ADR-0023: CSS Variable Theming Contract

> English (canonical) | [简体中文](../../zh/adr/0023-css-variable-theming.md)

- Status: Accepted
- Date: 2026-08-06
- Decision maker: Maintainer

## Context

Atlas ships a single stylesheet (`wwwroot/atlas-v2/atlas.css`, 286 lines) as a
Razor Class Library static asset. Consumers link it explicitly:

```html
<link rel="stylesheet" href="_content/Atlas.Blazor/atlas-v2/atlas.css" />
```

Today the stylesheet exposes only five internal variables
(`--atlas-splitter-size`, `--atlas-layer-content`, `--atlas-layer-chrome`,
`--atlas-layer-drag`); every color, radius, padding, and opacity value is
hard-coded (`Highlight`, `Canvas`, `CanvasText`, `currentColor`,
`rgb(0 0 0 / 28%)`, `color-mix(...)`, `0.25rem`, `2rem`, ...). An Application
Developer who wants a custom look has no supported mechanism: the only option
is overriding internal class selectors with `!important`, which is fragile and
breaks on every upgrade. SRS contains no requirement covering visual
customization.

Industry practice (MudBlazor `--mud-*`, Radzen `--rz-*`, Bootstrap 5,
Ant Design tokens) converges on CSS custom properties as the theming seam:
zero runtime cost, no JavaScript dependency, no build-chain change, and
upgrade-safe overrides (a library update that adds variables or changes
defaults does not break existing overrides as long as variables are never
removed).

## Decision

### 1. Tokenize the stylesheet

Every hard-coded visual value in `atlas.css` is expressed through a
documented `--atlas-*` custom property. Defaults are declared on the
`.atlas-v2-workspace` scope so they do not leak outside the component and a
consumer can override globally (any `.atlas-v2-workspace`) or per instance
(a descendant selector such as `#my-dock .atlas-v2-workspace`).

### 2. Variable contract (target)

Semantic names, not implementation names (`--atlas-accent`, not
`--atlas-blue`). Once published, the list is a public API: variables may be
added or have their defaults adjusted, but never removed.

Colors:

| Variable | Default | Used by |
| --- | --- | --- |
| `--atlas-accent` | `Highlight` | Drop-zone highlight, focus ring, drag-preview border |
| `--atlas-surface` | `Canvas` | Drag-preview background |
| `--atlas-text` | `CanvasText` | Drag-preview title text |
| `--atlas-drag-preview-shadow` | `rgb(0 0 0 / 28%)` | Drag-preview drop shadow |

Sizing:

| Variable | Default | Used by |
| --- | --- | --- |
| `--atlas-splitter-size` | `0.25rem` | Splitter thickness (existing) |
| `--atlas-splitter-hit-area` | `0.25rem` | Splitter pointer hit-area expansion |
| `--atlas-chrome-height` | `2rem` | Tab strip / panel header / empty-region height |
| `--atlas-gap` | `0.375rem` | Drag-preview internal gap |
| `--atlas-padding-compact` | `0.375rem 0.625rem` | Drag-preview padding |
| `--atlas-radius` | `0.25rem` | Regular border radius |
| `--atlas-radius-sm` | `0.125rem` | Small border radius (zone highlights) |
| `--atlas-border-width` | `1px` | Hairline borders (toolbar divider, drag preview) |
| `--atlas-focus-ring-width` | `2px` | Focus ring and drop-zone outline width |
| `--atlas-drag-preview-max-width` | `min(20rem, calc(100vw - 1rem))` | Drag-preview cap |

Layering (existing, kept):

| Variable | Default |
| --- | --- |
| `--atlas-layer-content` | `0` |
| `--atlas-layer-chrome` | `10` |
| `--atlas-layer-drag` | `30` |

Opacity (normalized during the refactor):

| Variable | Default | Used by |
| --- | --- | --- |
| `--atlas-splitter-opacity` | `0.18` | Splitter idle |
| `--atlas-splitter-opacity-hover` | `0.4` | Splitter hover / resize |
| `--atlas-splitter-opacity-focus` | `0.5` | Splitter focus-visible |
| `--atlas-splitter-opacity-disabled` | `0.08` | Splitter disabled |
| `--atlas-drop-highlight-alpha-soft` | `15%` | Generic (whole-element) drop highlight mix ratio |
| `--atlas-drop-highlight-alpha-strong` | `22%` | Zone-shaped drop highlight mix ratio |
| `--atlas-toolbar-divider-opacity` | `0.25` | Toolbar divider |
| `--atlas-drag-preview-opacity` | `0.92` | Drag preview |

The exact final list is locked during implementation; additions must keep the
defaults visually identical to the pre-change baseline.

### 3. Contract guarantees

- **Defaults unchanged**: with no overrides, rendering is identical to the
  baseline (verified in a real browser).
- **No `!important` path needed**: a consumer overrides variables only.
- **Two documented customization routes**: (a) variable overrides in the
  consumer's own stylesheet (recommended); (b) copy the stylesheet into the
  consumer project and edit it (deep customization, stops following upgrades).

## Alternatives

- **C# theme/options object (`AtlasTheme`)**. Rejected for now: it must
  ultimately produce CSS variables anyway, adds a second source of truth that
  can drift from the stylesheet, and runtime theme switching is not a current
  requirement. The variable contract keeps this path open later — an options
  object would simply be a producer of `--atlas-*` values.
- **SCSS source + multi-theme artifacts**. Rejected: introduces a Sass
  build chain, complicates the RCL static-asset story, and multiplies shipped
  artifacts. Revisit only if Atlas grows a full theme ecosystem (Bootstrap
  scale), on top of the variable contract.
- **Structural/skin stylesheet split**. Rejected at current scale: two linked
  files cost more coordination than they save until the stylesheet grows
  substantially. The split remains possible later without breaking the
  variable contract.

## Consequences

- **Behavior**: no functional change; purely a styling seam.
- **Contract**: the variable list becomes a published API; future versions
  must not remove variables.
- **Docs**: a theming guide (`docs/en/theming.md`) documents the variable
  list, the two customization routes, and a worked example.
- **Testing**: golden coverage for the default appearance (real browser) plus
  a variable-override check (accent change visible in drop highlight/focus).
- **Scope**: `atlas.css` only — no C#, no TypeScript, no protocol changes.

## Verification

- `AtlasWorkspaceHostTests`/bUnit remain green (no C# change).
- Node tests remain green (no protocol change).
- Real-browser fixture: default appearance unchanged; an
  `--atlas-accent` override changes the drop-zone highlight, focus ring, and
  drag-preview border without `!important`.
- `dotnet build/test/format` + `npm typecheck/test:js` full gates.

## Supersession

New decision; no ADR is superseded. It is the enabling decision for future
theme presets (`--atlas-*` override blocks) and an optional runtime theme
object.
