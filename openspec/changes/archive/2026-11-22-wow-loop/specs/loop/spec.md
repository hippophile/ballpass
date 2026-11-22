## ADDED Requirements

### Requirement: Interest lifecycle
The system SHALL record when an athlete saw an interest and how they replied.

#### Scenario: Opening the inbox marks seen
- **WHEN** an athlete opens /me/inbox
- **THEN** their unseen interests get SeenAt once; reopening does not change it

#### Scenario: Reply access control
- **WHEN** POST /me/inbox/{id}/reply is sent
- **THEN** missing antiforgery gives 400, anonymous redirects to /demo, a coach or a different athlete gives 403, only the owner may reply
- **AND** an accepted reply shows the demo club contact with a `.demo.invalid` address

### Requirement: Sent page
The system SHALL show a coach every sent interest with its state.

#### Scenario: Stepper
- **WHEN** a coach opens /me/sent
- **THEN** each sent interest shows Sent, Seen, Replied with states and timestamps, and the athlete chip links to the profile
- **AND** the nav link shows the number of unread replies

### Requirement: Persona switch and reset
The system SHALL let a demo user switch persona and reset the demo data when Demo:Enabled is true.

#### Scenario: Switch round trip
- **WHEN** a coach POSTs /demo/switch?to=athlete and then to=coach
- **THEN** they become the athlete of the coach's latest interest and then the same coach again
- **AND** Demo:Enabled=false hides and refuses it

#### Scenario: Reset
- **WHEN** POST /demo/reset runs twice
- **THEN** row counts are identical, the user is signed out and redirected to / with a toast
