# AVEX OS — Design System

**Status:** Phase 1 deliverable · **Source of truth for tokens**

**Read this first.** This document is largely a **target** specification, and only part of it is wired
into the shipped Control Center. Each section below says which. Verified against the code on
2026-11-08: the colour resources and the dark/light switch are real; the geometry resources, the
motion system, the focus/disabled rules, the accessibility contract, the grid and density modes, and
the icon standard are **not implemented**. Do not quote a line from here as evidence that something
exists — check [ARCHITECTURE.md](ARCHITECTURE.md) and
[COMPATIBILITY.md](../compatibility/COMPATIBILITY.md) first.

---

## 1. Intent

Sharp, compact, precise. Rectangular, thin-bordered, tightly spaced, high signal. No neon, no glass, no oversized controls, no decorative animation.

Explicitly rejected: macOS, Fluent-default, GNOME/KDE imitation. AVEX has its own geometry and a restrained accent.

---

## 2. Tokens

### Geometry

| Token | Value |
|---|---:|
| `radius.none` | 0 |
| `radius.sm` | 2 px |
| `radius.md` | 3 px |
| `radius.lg` | 5 px |
| `border.hairline` | 1 px |
| `space.xs` | 4 px |
| `space.sm` | 8 px |
| `space.md` | 12 px |
| `space.lg` | 16 px |
| `space.xl` | 24 px |
| `control.height` | 30 px (compact 26, comfortable 34) |
| `sidebar.width` | 208 px (range 190–220) |
| `motion.fast` | 130 ms |
| `motion.standard` | 165 ms |

**What of this is actually wired.** Only the **colour** rows exist as resources: the 14 dark and 14
light values are declared in
[`AvexTokens.xaml`](../../src/AVEXOS.App/Styles/AvexTokens.xaml) as `Color` + `SolidColorBrush` pairs
inside a `ThemeDictionaries` pair keyed `Dark`/`Light`, and the views consume them by
`{ThemeResource Avex…Brush}`. The colour values in the shipped XAML and in
[`avex-dark.json`](../../assets/themes/avex-dark.json) /
[`avex-light.json`](../../assets/themes/avex-light.json) agree today; nothing enforces that, so the
JSON is descriptive and the XAML is authoritative (see
[ARCHITECTURE.md §9](ARCHITECTURE.md)).

The geometry, `control.height`, `sidebar.width` and `motion` rows above are **specification, not
resources**. `AvexTokens.xaml` declares no `sys:Double` entries for them — and the one font resource it
does declare, `AvexFontFamily` at
[`AvexTokens.xaml:74`](../../src/AVEXOS.App/Styles/AvexTokens.xaml), is never referenced by any view,
so it is dead weight today. The views hardcode the
numbers instead — 63 `FontSize=`/`CornerRadius=`/`Padding=`/`Spacing=` occurrences across
`Views/*.xaml` (35 of them in `HomePage.xaml`) and `MainWindow.xaml`. So "every token is a resource,
no hard-coded pixel values in
views" is **not true today**; it is the rule to move toward, and a PR that adds a new literal should be
asked to add the resource instead.

The theme JSON files *do* carry `radius`/`space`/`motion`/`type` values, but no code reads them into
the visual tree — see the last paragraph of [ARCHITECTURE.md §9](ARCHITECTURE.md).

### Colour — AVEX Dark

| Token | Value | Use |
|---|---|---|
| `bg.base` | `#141517` | App background |
| `bg.raised` | `#191B1E` | Sidebar, headers |
| `surface.1` | `#1E2125` | Cards, rows |
| `surface.2` | `#24282D` | Hover |
| `border.subtle` | `#2C3137` | Dividers |
| `border.strong` | `#3A4048` | Input borders |
| `text.primary` | `#ECEDEF` | Body |
| `text.secondary` | `#A2A9B4` | Labels |
| `text.tertiary` | `#6E7681` | Captions |
| `accent` | `#6E8BFF` | Primary action, focus |
| `accent.hover` | `#8099FF` | |
| `success` | `#3FA96B` | |
| `warning` | `#D9A23A` | |
| `danger` | `#D9534F` | |

### Colour — AVEX Light

| Token | Value | Use |
|---|---|---|
| `bg.base` | `#F4F5F7` | App background |
| `bg.raised` | `#FAFAFB` | Sidebar, headers |
| `surface.1` | `#FFFFFF` | Cards |
| `surface.2` | `#EEF0F3` | Hover |
| `border.subtle` | `#E1E4E8` | Dividers |
| `border.strong` | `#C9CED6` | Input borders |
| `text.primary` | `#1C1F23` | Body |
| `text.secondary` | `#565D68` | Labels |
| `text.tertiary` | `#7C8591` | Captions |
| `accent` | `#2F5BEA` | |
| `accent.hover` | `#1F49D6` | |
| `success` / `warning` / `danger` | `#2E8B57` / `#B7791F` / `#C0392B` | |

Future collections (`Graphite`, `Minimal`, `Contrast`, `Atmosphere`, `Professional`, `Classic`) supply the same token keys — never new keys.

### Typography

| Token | Size / weight | Use |
|---|---|---|
| `type.display` | 28 / 600 | Page hero |
| `type.h1` | 20 / 600 | Page title |
| `type.h2` | 16 / 600 | Section |
| `type.h3` | 14 / 600 | Card title |
| `type.body` | 13 / 400 | Default |
| `type.body.secondary` | 13 / 400, secondary colour | Descriptions |
| `type.label` | 12 / 500 | Field labels |
| `type.caption` | 11 / 400, tertiary | Metadata |
| `type.mono` | 12 / 400, Cascadia Mono | Paths, IDs |

Family: `Segoe UI Variable Display/Text` → fallback `Segoe UI` → `sans-serif`. No decorative faces.

**[partly implemented]** The *sizes* above do appear in the rendered app — but as literals chosen to
match this table, not as resolved tokens, so nothing keeps them in sync. Counted across
`Views/*.xaml` and `MainWindow.xaml` on 2026-11-08: `FontSize="28"` ×1 (the Home hero), `"20"` ×4 (page
titles), `"16"` ×12 (sections), `"12"` ×7, `"11"` ×2. **`body` 13 and `h3` 14 are never set at all**, so
default-weight text renders at WinUI's own body size rather than the compact 13 px specified here.
`type.mono` / Cascadia Mono is **not used anywhere**: paths and IDs are shown in the UI's default face
(`ActivityPage`'s timestamps and the wallpaper path label included).

---

## 3. Components

Buttons (`primary`, `secondary`, `ghost`, `icon`, `danger`), menu, nav rail, sidebar, toolbar, tabs, search, text field, checkbox, radio, toggle, slider, progress, scrollbar, tooltip, notification, dialog, confirmation, title bar, context menu, file picker, settings panel, status bar, loading, empty, error, warning, success.

Rules:
- One control height per density mode; icon buttons are square at the same height.
- Focus ring: 2 px `accent`, 1 px inset offset — visible in both themes and in high contrast.
- Disabled state: 45 % opacity plus a shape change (never colour alone).
- State is always communicated by more than colour (icon or text).

**[not implemented]** Everything in the list above except six of those controls. What the shipped
views actually contain, counted on 2026-11-08: `TextBlock` (38), `StackPanel` (21), `Border` (10),
`ListView` (8), `ScrollViewer` (5), `FontIcon` (5), `Button` (5), `TextBox` (1) — plus `NavigationView`
in `MainWindow.xaml` and the single `ContentDialog` that gates approval
([`AppearancePage.xaml.cs:40`](../../src/AVEXOS.App/Views/AppearancePage.xaml.cs)). There is no
toggle, slider, checkbox, radio group, tabs, toolbar, search, tooltip, notification, progress, status
bar, context menu or file picker; wallpaper paths are typed, not picked. No custom button variants
(`primary`/`ghost`/`danger`) exist — the views use default WinUI styling, so the four Rules bullets
above are **not** in force: no focus-ring or disabled-state style is defined anywhere in the tree.

---

## 4. Motion

- Enter: fade 130 ms + 4 px translate, `cubic-bezier(0.2, 0, 0, 1)`.
- Exit: fade 100 ms, no translate.
- Theme switch: cross-fade 165 ms; no layout animation.
- Page navigation: 165 ms horizontal slide of 12 px inside a clipped container.
- Hover/press: background transition 130 ms.
- No looping decorative animation. No bounce. No spring overshoot.
- `SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)` and reduced-motion setting disable all non-essential motion.

**[not implemented]** None of the above runs. The tree contains zero `Storyboard`,
`VisualState`, `TransitionCollection` or enter/exit theme-transition declarations, no `AnimationState`
or reduced-motion query in our code (the only matches are inside WindowsAppSDK binaries), and
`SPI_GETCLIENTAREAANIMATION` is never called. What you see today is WinUI's own default page-transition
behaviour, which is **not** the 165 ms 12 px slide specified here.

---

## 5. Accessibility contract

- Full keyboard reachability with a visible focus ring on every interactive element.
- `AutomationProperties.Name` on every icon-only control.
- Contrast ≥ 4.5:1 for text, ≥ 3:1 for UI boundaries; verified for both themes.
- Layout correct at 100/125/150/200 % scaling and up to 200 % text scaling.
- High-contrast mode: borders and focus remain visible; no information lost.
- Live regions for async status ("Applied", "Verified", "Rolled back").
- Never rely on colour alone to signal capability level (A–D) — always pair with a label.

**[partly implemented, mostly unverified]** On 2026-11-08 the tree contains **zero**
`AutomationProperties.Name` attributes, so any icon-only control is unnamed to a screen reader unless
the platform derives a name for it. The five `FontIcon`s shipped sit inside `NavigationViewItem`s that
carry `Content` strings, which is the good case — but **no screen reader was run against this app**, so
treat every accessibility line above as unproven. Keyboard tabbing works because it is stock WinUI, not
because it was designed; the 2 px accent focus ring is not defined anywhere. The contrast ratios are
**not measured** for either theme, high-contrast behaviour has no code in the tree (the framework's own
handling is assumed), scaling at 100/125/150/200 % has never been exercised, and there is no
live-region equivalent: `StatusText` labels are updated in place without an `AutomationLiveSetting`, so
an async result is not announced. The last bullet is the one place the rule holds by accident —
`ApplicationsPage` shows the reason sentence rather than a coloured letter, because the letter is never
rendered at all.

---

## 6. Layout

- Sidebar 208 px, content max 1180 px, 24 px gutters.
- 12-column content grid, 16 px gutters.
- Density modes: Compact / Standard / Comfortable change `space.*` and `control.height` only.
- Every list view degrades to a single column below 900 px.

**[not implemented]** Not one of these four numbers is set in the app. `MainWindow.xaml` overrides
neither `OpenPaneLength` nor `CompactPaneLength`, so the sidebar is WinUI's default width; no view sets
`MaxWidth`, so content fills the window rather than capping at 1180 px; the only `Grid` in the tree is
[`HomePage.xaml:10-15`](../../src/AVEXOS.App/Views/HomePage.xaml), a **four**-column `Width="*"` row of
stat cards, not a 12-column content grid; every other page is a `StackPanel` in a `ScrollViewer` with
`Spacing=` literals. There are no density modes (no code reads or stores one) and no adaptive triggers,
so a narrow window does not collapse a list — it clips it.

---

## 7. Icon standard

24 px grid; 2 px keyline padding; 1.75 px stroke at 24 px; flat, monochrome-capable, no gradients;
distinct silhouette; light and dark variants where contrast requires it; `.ico` exports at
16/20/24/32/48/64/128/256 for shell targets.

**[not implemented]** This is a brief for a designer, not a description of the tree. `assets/branding/`
and `assets/icons/` are **empty directories**, and there is **no `.ico` file anywhere in the
repository** (verified with a find over the whole tree on 2026-11-08, `obj/` excluded). Every glyph the
Control Center shows is a Segoe Fluent Icons codepoint in a `FontIcon` (`&#xE80F;`, `&#xE790;`,
`&#xE771;`, `&#xE71D;`, `&#xEA37;`), i.e. Microsoft's icons, not AVEX's. That is deliberate — the
`Symbol` enum is a reduced WinUI 3 subset and `Icon="Color"` throws a `XamlParseException` at load
(see [TROUBLESHOOTING.md §1](../operations/TROUBLESHOOTING.md)) — but it means no AVEX iconography
exists yet, and the shell-target exports in the last clause would be a Level D feature even if the
files were there.
