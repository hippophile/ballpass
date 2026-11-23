# demo-data

## MODIFIED Requirements

### Requirement: Reset restores interests and shortlists

The system SHALL restore exactly the generated interests and shortlist entries on every reset, plus the accepted interests that the
messages seed adds for its conversations.

#### Scenario: Reset twice keeps generated counts

- **WHEN** the reset runs twice on an empty database
- **THEN** the interest count equals the generated count plus the messages seed's extra interests, and the shortlist count equals the generated count
