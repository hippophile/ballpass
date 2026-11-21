# coach-search Specification

## Purpose
TBD - created by archiving change add-demo-login-and-search. Update Purpose after archive.

## Requirements

### Requirement: Coach search with filters

The system SHALL let a signed-in coach search athletes by gender, position, height range, age range, region, league, looking-for-team and name, showing the total count and 20 results per page.

#### Scenario: Default search shows women

- **GIVEN** a signed-in coach
- **WHEN** they open `/search` without parameters
- **THEN** the total equals the number of women athletes in the demo data (200)
- **AND** 20 result cards are shown

#### Scenario: Position and minimum height

- **WHEN** the coach searches with `position=C&minHeight=185`
- **THEN** the total equals the number of women centers at least 185 cm tall in the demo data
- **AND** every result card links to such an athlete

#### Scenario: Looking for team

- **WHEN** the coach searches with `looking=true`
- **THEN** the total equals the number of women athletes looking for a team

#### Scenario: Region, league and age

- **WHEN** the coach filters by `region`, by `league`, or by `minAge`/`maxAge`
- **THEN** each total equals the matching count computed from the demo data

#### Scenario: Name search

- **WHEN** the coach searches with a surname as `q`
- **THEN** every result has that surname

#### Scenario: Paging

- **WHEN** the coach requests `page=2` of the default search
- **THEN** the 20 results differ from page 1
