## ADDED Requirements

### Requirement: Deterministic fictional demo data

The system SHALL generate the demo data set from a fixed seed so that every generation produces an identical set of clubs, coaches, athletes, seasons and stat lines.

#### Scenario: Same seed gives same data

- **GIVEN** the demo data generator with seed 42
- **WHEN** it generates the data set twice
- **THEN** both sets contain the same athletes in the same order with the same names, heights, positions and stats

#### Scenario: Expected volumes

- **WHEN** the generator runs with seed 42
- **THEN** it produces 200 women athletes, 100 men athletes, 20 clubs (12 women, 8 men) and 30 coaches
- **AND** every athlete has at least one stat line and at least one club season

### Requirement: Demo data is clearly fictional and adult-only

The system MUST flag every generated club, coach and athlete as demo data, MUST suffix every club name with "(demo)", and MUST NOT generate any athlete younger than 18 on 2026-01-01.

#### Scenario: Flags and labels

- **WHEN** the generator runs
- **THEN** every club, coach and athlete has IsDemo = true
- **AND** every club name ends with " (demo)"
- **AND** no athlete has a video URL

#### Scenario: Adults only

- **WHEN** the generator runs
- **THEN** every athlete's age on 2026-01-01 is between 18 and 34 inclusive

### Requirement: Plausible athlete data

The system SHALL produce athlete attributes and stats within plausible basketball ranges, with Greek names in the correct grammatical gender and unique Latin URL slugs.

#### Scenario: Ranges

- **WHEN** the generator runs
- **THEN** women's heights are 160–200 cm and men's 175–220 cm
- **AND** per-game points are 0–30, rebounds 0–15, assists 0–10, and shooting percentages are between 0 and 100

#### Scenario: Names and slugs

- **WHEN** the generator runs
- **THEN** every women athlete's last name is in the female form (ends in a, η, ου or ω-based female suffix from the name list)
- **AND** all athlete slugs are unique, lowercase Latin letters, digits and hyphens

#### Scenario: Stat source labels

- **WHEN** the generator runs
- **THEN** every stat line has a source of SelfReported, CoachConfirmed or LeagueVerifiedMock
- **AND** each of the three sources is used at least once

### Requirement: Idempotent demo reset

The system SHALL provide a reset command that applies migrations, deletes all demo rows and inserts the generated data set in one transaction.

#### Scenario: Reset twice

- **GIVEN** an empty test database
- **WHEN** the reset runs twice
- **THEN** the database contains exactly 300 athletes, 20 clubs and 30 coaches
