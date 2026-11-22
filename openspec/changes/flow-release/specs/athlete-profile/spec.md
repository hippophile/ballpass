## MODIFIED Requirements

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
