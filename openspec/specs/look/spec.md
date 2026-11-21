# look Specification

## Purpose
TBD - created by archiving change wow-look. Update Purpose after archive.

## Requirements

### Requirement: Single dark theme

The system SHALL render every page with `data-theme="dark"` and `data-bs-theme="dark"` on the `<html>` element, with no light theme, accent variant or theme switch.

#### Scenario: Every MVP route is dark

- **WHEN** a visitor or signed-in persona requests `/`, `/demo`, `/search`, `/p/{slug}`, `/shortlist`, `/me/inbox`, `/denied` or an unknown path
- **THEN** the `<html>` element carries `data-theme="dark"` and `data-bs-theme="dark"`
- **AND** the demo banner (`role="status"`, Greek and English text) is still present

### Requirement: Self-hosted assets only

The system SHALL load fonts, stylesheets and scripts only from its own origin, with no runtime request to an external host.

#### Scenario: No external hosts

- **WHEN** any page is requested
- **THEN** no `<link>` or `<script>` references an `http://` or `https://` URL
- **AND** no CSS file served by the app contains an `@font-face` `src` or `url()` to an external host

### Requirement: Accessible design tokens

The design tokens in `wwwroot/css/bp-tokens.css` MUST meet WCAG contrast: text tokens at least 4.5:1 and UI-border tokens at least 3:1 on the surfaces they are used on.

#### Scenario: Text tokens

- **WHEN** the contrast of `--bp-text`, `--bp-muted`, `--bp-accent`, `--bp-success` and `--bp-src-self|coach|league` is computed against `--bp-bg`, `--bp-s1` and `--bp-s2`
- **THEN** every ratio is at least 4.5

#### Scenario: UI border and button text

- **WHEN** `--bp-border-ui` is computed against `--bp-bg`, `--bp-s1` and `--bp-s2`
- **THEN** every ratio is at least 3
- **AND** `--bp-accent-contrast` on `--bp-accent` is at least 4.5

### Requirement: Source badges carry icon and text

The system SHALL render every stat source badge with an icon element and a text label, and the league-verified badge SHALL contain the word "mock" in both languages.

#### Scenario: Badge anatomy

- **WHEN** an athlete profile with stats is requested in `el` or `en`
- **THEN** each source badge contains an `<svg>` icon and a text label
- **AND** the league-verified badge text contains "mock" (case-insensitive)

### Requirement: Own components do not collide with Bootstrap

The system SHALL name its own card and badge components `bp-card` and `bp-badge`, and SHALL disable all motion (hover lift, count-up, view transition) under `prefers-reduced-motion`.

#### Scenario: Reduced motion

- **WHEN** the user agent reports `prefers-reduced-motion: reduce`
- **THEN** no transition, animation or view transition runs, and counters show their final value
