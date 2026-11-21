## ADDED Requirements

### Requirement: Adults on every day of the demo year

The system MUST NOT generate any athlete who is younger than 18 on any day of 2026, so every athlete's birth year is 2007 or earlier.

#### Scenario: Worst-case age

- **WHEN** the generator runs with seed 42
- **THEN** for every athlete, 2026 minus birth year minus 1 is at least 18

### Requirement: Reset restores interests and shortlists

The system SHALL restore exactly the generated interests and shortlist entries on every reset.

#### Scenario: Reset twice keeps generated counts

- **WHEN** the reset runs twice on an empty database
- **THEN** the interest and shortlist counts equal the generated counts
