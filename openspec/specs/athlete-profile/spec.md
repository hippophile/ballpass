# athlete-profile Specification

## Purpose
TBD - created by archiving change add-profile-and-i18n. Update Purpose after archive.

## Requirements

### Requirement: Public athlete profile page

The system SHALL serve a public, read-only athlete profile at `/p/{slug}` showing identity, physical data, club, season stats with a source label per row, club history, bio, a video placeholder and the shareable URL, without a "DEMO" badge (SPEC §8 O1).

#### Scenario: Existing athlete with a club

- **GIVEN** the demo data set generated with seed 42
- **WHEN** a client requests `/p/{slug}` for an athlete with a current club
- **THEN** the response status is 200
- **AND** the page shows the athlete's Greek full name, height in cm, position abbreviation and current club name
- **AND** the page shows one stats row per season of that athlete
- **AND** the page shows the shareable URL path `/p/{slug}`
- **AND** the page has no `bp-badge-demo` element

#### Scenario: Stat source labels

- **GIVEN** an athlete with a coach-confirmed stat line
- **WHEN** the profile is requested in Greek
- **THEN** the page shows the label "Επιβεβαιωμένο από προπονητή"

#### Scenario: Looking for team

- **GIVEN** an athlete without a current club
- **WHEN** the profile is requested
- **THEN** the page shows the "looking for team" badge

#### Scenario: Unknown athlete

- **WHEN** a client requests `/p/does-not-exist`
- **THEN** the response status is 404
- **AND** the footer disclosure is shown

### Requirement: Social preview metadata

The system SHALL include Open Graph metadata on every athlete profile so that shared links render a preview card.

#### Scenario: og:title present

- **WHEN** a profile is requested
- **THEN** the HTML head contains a `meta` tag with `property="og:title"` whose content includes the athlete's full name

### Requirement: Profile cover, proof row and action rail

The profile SHALL open with a cover (stock no-face photo by slug, eager, `--bp-scrim` + `--bp-scrim-left`) carrying a 120 px jersey avatar, the name, the teal `.bp-chip-athlete` "Ψάχνει ομάδα" and one meta line; then a proof row (3 latest per-game numbers in accent-hi, season + source, the two best percentiles when the profile has them), highlights, the stats card (evidence + table) and one history/bio card. A sticky action rail SHALL hold, for a coach, the only `.bp-btn-primary` ("Εκδήλωση ενδιαφέροντος"), the note `<details>`, a ghost row (Λίστα · Ακολουθήστε · Σύγκριση) and "Κοινοποίηση" whose share card (og.png) only exists inside a closed `<details>`. Under 640 px the coach rail is a fixed bottom bar with the primary, a ✎ note toggle and a "⋯" sheet.

#### Scenario: Coach view
- **WHEN** a coach without an interest opens a profile
- **THEN** the page has exactly one `.bp-btn-primary`, inside `aside.bp-action-rail`, and the og.png preview sits in a `<details class="bp-rail-share">` without `open`

#### Scenario: Anonymous view
- **WHEN** an anonymous visitor opens a profile
- **THEN** there is no `.bp-btn-primary` and no interest form; the share details is present

### Requirement: Sent rail

When the coach has an interest for the athlete (including `?status=interest-sent` / `interest-exists`), the rail SHALL show a teal-bordered card "✓ Στάλθηκε · {date}", a 3-dot stepper (Στάλθηκε, Το είδε, Απάντηση), a teal-outline form POST `/demo/switch?to=athlete` "Δείτε τι βλέπει η/ο {FirstName} →" (only when `Demo:Enabled` and this athlete is the coach's latest interest, which is who the switch signs in as) and a link to `/me/sent`. No status line is shown for those two statuses.

#### Scenario: After sending
- **WHEN** a coach sends an interest and lands on `/p/{slug}?status=interest-sent`
- **THEN** the rail has `data-rail="sent"`, the switch form with an antiforgery token, the `/me/sent` link, no interest form and no `.bp-btn-primary`

### Requirement: Highlights without empty tiles

Highlights SHALL render only existing items: one main tile and up to two thumbs. The featured athlete (`Demo:Featured:AthleteSlug`) SHALL get the three no-face stock clips (`StockMedia.HighlightMain/Thumb1/Thumb2`, SPEC §8 O9). Every tile SHALL have an `img[src]` or a `video[poster]`.

#### Scenario: Featured highlights
- **WHEN** the featured profile is opened
- **THEN** `#highlights` has 3 tiles with the three stock clips and the "no video yet" text is absent
