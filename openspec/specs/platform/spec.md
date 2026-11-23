# platform Specification

## Purpose
TBD - created by archiving change add-skeleton. Update Purpose after archive.

## Requirements

### Requirement: Health endpoint reports database reachability

The system SHALL expose `GET /health` that returns HTTP 200 when the PostgreSQL database is reachable and HTTP 503 when it is not.

#### Scenario: Database reachable

- **GIVEN** the app is configured with a valid `ConnectionStrings:Ballpass`
- **WHEN** a client requests `GET /health`
- **THEN** the response status is 200
- **AND** the body is `Healthy`

#### Scenario: Database unreachable

- **GIVEN** the app is configured with a connection string pointing to a port where no database listens
- **WHEN** a client requests `GET /health`
- **THEN** the response status is 503

### Requirement: Configuration without secrets in source

The system MUST read the database connection string only from configuration key `ConnectionStrings:Ballpass` supplied by user-secrets or environment variables, never from tracked files.

#### Scenario: No connection string in tracked config

- **WHEN** the tracked `appsettings*.json` files are inspected
- **THEN** none of them contains a `Ballpass` connection string value

### Requirement: One fictional-data disclosure

The system SHALL render on every HTML page exactly one footer line, 12 px in `--bp-faint`, reading "Φανταστικά πρόσωπα και δεδομένα" (Greek) or "Fictional people and data" (English), and SHALL NOT render any other "demo", "δείγμα", "seeded" or "fictional" label in visible text, except the `/demo` intro sentence.

#### Scenario: Home page shows the footer line
- **WHEN** a client requests `GET /`
- **THEN** the response status is 200
- **AND** the HTML contains `<footer class="bp-footer">` with "Φανταστικά πρόσωπα και δεδομένα"
- **AND** the HTML contains no element with class `demo-banner`

#### Scenario: Not-found page shows the footer line
- **WHEN** a client requests a path that does not exist
- **THEN** the HTML still contains the footer disclosure and no demo banner

#### Scenario: No demo words in visible text
- **WHEN** `/`, `/feed`, `/match` (coach), `/p/{slug}`, `/shortlist` (coach), `/me/inbox` (athlete) or `/me/sent` (coach) is requested in `el` or `en`
- **THEN** the visible text (tags, scripts, styles and attributes stripped) outside the footer contains none of "demo", "δείγμα", "seeded", "επίδειξ" (case-insensitive)
