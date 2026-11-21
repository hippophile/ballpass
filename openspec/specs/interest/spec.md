# interest Specification

## Purpose
TBD - created by archiving change add-interest-and-shortlist. Update Purpose after archive.

## Requirements

### Requirement: Coach expresses interest

The system SHALL let a signed-in coach express interest in an athlete from the athlete's profile, with an optional note of at most 280 characters, and SHALL show it in that athlete's inbox exactly once per coach.

#### Scenario: Interest reaches the inbox once

- **GIVEN** a signed-in coach on an athlete's profile
- **WHEN** the coach posts an interest for that athlete twice
- **THEN** the athlete's inbox lists that coach exactly once
- **AND** after the first post the profile shows an "already sent" confirmation instead of the form

#### Scenario: Note too long

- **WHEN** the coach submits a note of 281 characters
- **THEN** no interest is stored for that pair

### Requirement: Actions are role-gated

The system MUST show interest and shortlist actions only to signed-in coaches and MUST reject those actions from other roles.

#### Scenario: Anonymous visitor sees no actions

- **WHEN** an anonymous visitor opens a profile
- **THEN** no interest or shortlist form is present

#### Scenario: Athlete cannot express interest

- **GIVEN** a signed-in athlete
- **WHEN** they post to `/p/{slug}/interest`
- **THEN** the response status is 403

### Requirement: Seeded interest data

The system SHALL seed deterministic demo interests (40 in total) and shortlists (3 to 5 per head coach).

#### Scenario: Seed counts

- **WHEN** the generator runs with seed 42
- **THEN** it produces 40 interests and each head coach has between 3 and 5 shortlist entries
