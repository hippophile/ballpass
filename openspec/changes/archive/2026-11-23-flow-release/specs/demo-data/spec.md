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

## ADDED Requirements

### Requirement: Plausible per-minute stat lines

Generated stat lines SHALL be plausible for the minutes played: per 40 minutes, points ≤ 32, rebounds ≤ 20, assists ≤ 12, steals ≤ 5 and blocks ≤ 4 (our estimate, not a league rule). A line over any cap SHALL be scaled down by one factor for all five counting stats, applied after all random draws so seed 42 stays deterministic and lines within the caps are unchanged.

#### Scenario: Caps
- **WHEN** the generator runs with seed 42
- **THEN** no stat line exceeds any per-40 cap

#### Scenario: Unchanged lines
- **WHEN** a line is within every cap (for example 30+ minutes)
- **THEN** its per-game numbers are the same as before the cap was introduced
