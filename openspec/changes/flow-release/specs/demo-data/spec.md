## MODIFIED Requirements

### Requirement: Demo data is clearly fictional and adult-only

The system MUST flag every generated club, coach and athlete as demo data (`IsDemo`), MUST NOT add a "(demo)" suffix or any other demo tag to visible names (SPEC §8 O1), and MUST NOT generate any athlete younger than 18 on 2026-01-01.

#### Scenario: Flags and labels

- **WHEN** the generator runs
- **THEN** every club, coach and athlete has IsDemo = true
- **AND** no club name contains "(demo)"
- **AND** no athlete has a video URL

#### Scenario: Adults only

- **WHEN** the generator runs
- **THEN** every athlete's age on 2026-01-01 is between 18 and 34 inclusive
