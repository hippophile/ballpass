# athlete-profile Specification

## Purpose
TBD - created by archiving change add-profile-and-i18n. Update Purpose after archive.

## Requirements

### Requirement: Public athlete profile page

The system SHALL serve a public, read-only athlete profile at `/p/{slug}` showing identity, physical data, club, season stats with a source label per row, club history, bio, a video placeholder and the shareable URL.

#### Scenario: Existing athlete with a club

- **GIVEN** the demo data set generated with seed 42
- **WHEN** a client requests `/p/{slug}` for an athlete with a current club
- **THEN** the response status is 200
- **AND** the page shows the athlete's Greek full name, height in cm, position abbreviation and current club name
- **AND** the page shows one stats row per season of that athlete
- **AND** the page shows the shareable URL path `/p/{slug}`

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
- **AND** the demo banner is shown

### Requirement: Social preview metadata

The system SHALL include Open Graph metadata on every athlete profile so that shared links render a preview card.

#### Scenario: og:title present

- **WHEN** a profile is requested
- **THEN** the HTML head contains a `meta` tag with `property="og:title"` whose content includes the athlete's full name
