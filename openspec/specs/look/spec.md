# look Specification

## Purpose
TBD - created by archiving change wow-look. Update Purpose after archive.

## Requirements

### Requirement: Single dark theme

The system SHALL render every page with `data-theme="dark"` and `data-bs-theme="dark"` on the `<html>` element, with no light theme, accent variant or theme switch.

#### Scenario: Every MVP route is dark

- **WHEN** a visitor or signed-in persona requests `/`, `/demo`, `/search`, `/p/{slug}`, `/shortlist`, `/me/inbox`, `/denied` or an unknown path
- **THEN** the `<html>` element carries `data-theme="dark"` and `data-bs-theme="dark"`
- **AND** there is no demo banner and the footer carries the fictional-data disclosure

### Requirement: Self-hosted assets only

The system SHALL load fonts, stylesheets and scripts only from its own origin, with no runtime request to an external host.

#### Scenario: No external hosts

- **WHEN** any page is requested
- **THEN** no `<link>` or `<script>` references an `http://` or `https://` URL
- **AND** no CSS file served by the app contains an `@font-face` `src` or `url()` to an external host

### Requirement: Accessible design tokens

The design tokens in `wwwroot/css/bp-tokens.css` MUST meet WCAG contrast: text tokens at least 4.5:1 and UI-border tokens at least 3:1 on the surfaces they are used on, and the computed ratios MUST equal the flow-release table (±0.01).

#### Scenario: Text tokens

- **WHEN** the contrast of `--bp-text`, `--bp-muted`, `--bp-faint`, `--bp-accent`, `--bp-accent-hi`, `--bp-athlete`, `--bp-danger`, `--bp-success` and `--bp-src-self|coach|league` is computed against `--bp-bg`, `--bp-s1` and `--bp-s2`
- **THEN** every ratio is at least 4.5
- **AND** text on bg/s1/s2/s3 is 18.18/16.44/14.22/11.14, muted 8.96/8.10/7.01/5.49, faint 7.52/6.80/5.88/4.61, accent 6.90/6.23/5.39/4.22, accent-hi 8.44/7.63/6.60/5.17, athlete 10.59/9.57/8.28/6.48, danger 8.03/7.26/6.28/4.92, src-coach 7.75/7.01/6.06/4.75

#### Scenario: UI border and button text

- **WHEN** `--bp-border-ui` is computed against `--bp-bg`, `--bp-s1`, `--bp-s2` and `--bp-s3`
- **THEN** the ratios are 5.86/5.29/4.58/3.59
- **AND** `--bp-accent-contrast` on accent-hi/accent/accent-lo is 8.44/6.90/5.69 and `--bp-athlete-contrast` on athlete-hi/athlete/athlete-lo is 11.56/9.18/6.87

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

### Requirement: Text over photos sits on a scrim

Every component that puts text on a photo or video (`StockPhoto`, `HeroVideo`) SHALL render a scrim layer, and the scrim's text zone (alpha at least .80 over `rgb(5 7 11)`) SHALL keep text and muted text at least 4.5:1 even over pure white pixels.

#### Scenario: Scrim over white
- **WHEN** `--bp-scrim` at its 35% stop is composited over `#FFFFFF`
- **THEN** `--bp-text` and `--bp-muted` on the result are both at least 4.5

### Requirement: One page width and aligned shell

The shell SHALL use one content width (`--bp-w-page` 1280 px, 32 px side padding, 16 px under 640 px) on every page, and the topbar SHALL align the logo with the content's left edge.

#### Scenario: Width token used by the shell
- **WHEN** the layout CSS is read
- **THEN** `.content` uses `max-width:var(--bp-w-page)` and the topbar padding uses `calc((100% - var(--bp-w-page))/2 + 32px)`

### Requirement: Role navigation with a user menu

The topbar SHALL show coaches exactly 4 primary links (Ταίριασμα, Λίστα, Αποστολές, Μηνύματα) and athletes exactly 3 (Εισερχόμενα, Μηνύματα, Το προφίλ μου); feed, search, the persona name and sign-out SHALL live in a `<details class="bp-usermenu">` whose summary is a 32 px jersey avatar. The persona switch pill and the EL|EN toggle stay visible.

#### Scenario: Coach nav
- **WHEN** a coach requests any page
- **THEN** the primary nav has 4 links: `/match`, `/shortlist`, `/me/sent`, `/messages`
- **AND** the user menu contains `/feed`, `/search` and the sign-out form

#### Scenario: Athlete nav
- **WHEN** an athlete requests any page
- **THEN** the primary nav has 3 links: `/me/inbox`, `/messages` and `/p/{own-slug}`

### Requirement: Button and card primitives

`bp-base.css` SHALL define `.bp-btn-primary` (orange gradient, dark ink), `.bp-btn-athlete` (teal gradient, `--bp-athlete-contrast` ink), `.bp-btn-outline`, `.bp-btn-ghost`, `.bp-chip-athlete`, `.bp-card`, `.bp-photo`, `.bp-photo-scrim` and `.bp-stepper`; white text on orange is never used.

#### Scenario: Primitives exist
- **WHEN** `bp-base.css` is read
- **THEN** every primitive above has a rule, `.bp-btn-primary` uses `--bp-grad-cta` and `--bp-accent-contrast`, `.bp-btn-athlete` uses `--bp-grad-athlete` and `--bp-athlete-contrast`
