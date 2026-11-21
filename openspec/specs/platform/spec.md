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

### Requirement: Demo banner on every page

The system SHALL render a demo banner stating that all athletes, coaches and clubs are fictional, in Greek and English, on every HTML page.

#### Scenario: Home page shows the banner

- **WHEN** a client requests `GET /`
- **THEN** the response status is 200
- **AND** the HTML contains an element with `role="status"` and class `demo-banner`
- **AND** the banner text contains "all athletes, coaches and clubs are fictional"
- **AND** the banner text contains "φανταστικοί"

#### Scenario: Not-found page shows the banner

- **WHEN** a client requests a path that does not exist
- **THEN** the HTML response still contains the demo banner

### Requirement: Configuration without secrets in source

The system MUST read the database connection string only from configuration key `ConnectionStrings:Ballpass` supplied by user-secrets or environment variables, never from tracked files.

#### Scenario: No connection string in tracked config

- **WHEN** the tracked `appsettings*.json` files are inspected
- **THEN** none of them contains a `Ballpass` connection string value
