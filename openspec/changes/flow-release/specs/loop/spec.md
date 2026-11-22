## MODIFIED Requirements

### Requirement: Interest lifecycle
The system SHALL record when an athlete saw an interest and how they replied.

#### Scenario: Opening the inbox marks seen
- **WHEN** an athlete opens /me/inbox
- **THEN** their unseen interests get SeenAt once; reopening does not change it

#### Scenario: Reply access control
- **WHEN** POST /me/inbox/{id}/reply is sent
- **THEN** missing antiforgery gives 400, anonymous redirects to /demo, a coach or a different athlete gives 403, only the owner may reply
- **AND** an accepted reply shows the club contact with a reserved `.invalid` address (never a real mailbox), without a "(demo)" tag or a "fictional address" note
