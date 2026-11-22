## ADDED Requirements

### Requirement: Every seeded row is flagged demo

Kudos, Follow, Block and Report SHALL carry `IsDemo` (true for seeded and for user-created rows). The seeder SHALL use the injected `TimeProvider`.

#### Scenario: Flags
- **WHEN** the seeded kudos and follows are read, and rows are created through the endpoints
- **THEN** all seeded kudos and follows have `IsDemo = true`, and rows created through the endpoints have it too

#### Scenario: Clock
- **WHEN** the seeder runs with a fixed `TimeProvider`
- **THEN** no seeded timestamp is later than that time
