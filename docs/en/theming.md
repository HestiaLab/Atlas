# Atlas Styling and Theming

> English (canonical) | [简体中文](../zh/theming.md)

Atlas ships one stylesheet (`_content/Atlas.Blazor/atlas-v2/atlas.css`) and
exposes its visual style through documented CSS custom properties prefixed
with `--atlas-` (REQ-NF-009, ADR-0023). You customize the look by overriding
these variables — no `!important`, no internal selectors.

## 1. Link the stylesheet

```html
<link rel="stylesheet" href="_content/Atlas.Blazor/atlas-v2/atlas.css" />
```

This loads the default theme. The defaults use CSS system colors
(`Highlight`, `Canvas`, `CanvasText`), which the browser resolves to
platform interface colors — on Windows/Chrome, for example, `Highlight`
resolves to the system accent color and `Canvas`/`CanvasText` resolve to
the light-mode white/black. They follow the OS light/dark preference only
when the browser is configured to follow the system theme, and the exact
resolved values depend on the browser and platform.

## 2. Override variables (recommended)

Declare overrides in your own stylesheet, **after** the Atlas link. The
defaults live on the `.atlas-v2-workspace` element, so any selector that
matches a workspace container (or an ancestor) wins.

### Worked example: change the accent color

```css
/* app.css — loaded after atlas.css */
.atlas-v2-workspace {
  --atlas-accent: #d33;
}
```

The drop-zone highlight, focus ring, and drag-preview border now use `#d33`.

### Worked example: dark surface + wider splitters

```css
.atlas-v2-workspace {
  --atlas-accent: #4c9aff;
  --atlas-surface: #1e1e1e;
  --atlas-text: #d4d4d4;
  --atlas-splitter-size: 0.375rem;
  --atlas-splitter-opacity: 0.35;
  --atlas-splitter-opacity-hover: 0.6;
}
```

### Worked example: restyle only one instance

```css
#my-dock .atlas-v2-workspace {
  --atlas-accent: #2e7d32;
}
```

## 3. Variable contract

The list below is a public API: variables may be added or have their defaults
adjusted in future versions, but never removed.

### Colors

| Variable | Default | Used by |
| --- | --- | --- |
| `--atlas-accent` | `Highlight` | Drop-zone highlight, focus ring, drag-preview border |
| `--atlas-surface` | `Canvas` | Drag-preview background |
| `--atlas-text` | `CanvasText` | Drag-preview title text |
| `--atlas-drag-preview-shadow` | `rgb(0 0 0 / 28%)` | Drag-preview drop shadow |
| `--atlas-group-bg` | `transparent` | Group surface background |
| `--atlas-chrome-bg` | `transparent` | Tab strip / panel header / toolbar background |
| `--atlas-tab-text` | `inherit` | Tab label color |
| `--atlas-tab-text-active` | `inherit` | Active tab label color |
| `--atlas-tab-bg-active` | `transparent` | Active tab background |
| `--atlas-tab-bg-hover` | `transparent` | Hovered tab background |
| `--atlas-splitter-color` | `currentColor` | Splitter bar color |

### Sizing

| Variable | Default | Used by |
| --- | --- | --- |
| `--atlas-splitter-size` | `0.25rem` | Splitter thickness |
| `--atlas-splitter-hit-area` | `0.25rem` | Splitter pointer hit-area expansion |
| `--atlas-chrome-height` | `2rem` | Tab strip / panel header / empty-region height |
| `--atlas-gap` | `0.375rem` | Drag-preview internal gap |
| `--atlas-padding-compact` | `0.375rem 0.625rem` | Drag-preview padding |
| `--atlas-radius` | `0.25rem` | Regular border radius |
| `--atlas-radius-sm` | `0.125rem` | Small border radius (zone highlights) |
| `--atlas-border-width` | `1px` | Hairline borders (toolbar divider, drag preview) |
| `--atlas-focus-ring-width` | `2px` | Focus ring and drop-zone outline width |
| `--atlas-drag-preview-max-width` | `min(20rem, calc(100vw - 1rem))` | Drag-preview cap |

### Layering

| Variable | Default |
| --- | --- |
| `--atlas-layer-content` | `0` |
| `--atlas-layer-chrome` | `10` |
| `--atlas-layer-drag` | `30` |

### Opacity

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

## 4. Deep customization (copy the stylesheet)

If variable overrides are not enough, copy `atlas.css` into your project,
edit it, and link your copy instead of the package asset:

```html
<link rel="stylesheet" href="css/atlas.css" />
```

This gives full control over layout and structure, but your copy stops
following Atlas upgrades — future releases may change class names or structure.
Prefer variable overrides unless you need structural changes.

## 5. Compatibility notes

- Defaults are visually identical to the pre-ADR-0023 baseline.
- Variables are scoped to `.atlas-v2-workspace`; overrides on any ancestor
  propagate by CSS inheritance.
- `currentColor` text/border values are not tokenized: they intentionally
  follow the host application's text color.
- Removed variables are a breaking change; the contract is additive.
- The browser resolves the system-color defaults (`Canvas`, `CanvasText`,
  `Highlight`) under a light or dark palette selected by the `color-scheme`
  property (CSS Color Adjustment) — not by `prefers-color-scheme` media-query
  emulation. Measured on Windows/Chrome: `light` resolves `Canvas`
  `#FFFFFF`, `CanvasText` `#000000`, `Highlight` `#0078D4` (system accent);
  `dark` resolves `Canvas` `#121212`, `CanvasText` `#FFFFFF`, `Highlight`
  `#1967D2` (browser-defined dark accent). To render the Atlas defaults in a
  dark palette without overriding variables, declare
  `:root { color-scheme: dark }` (or `light dark`) in the consumer app.
