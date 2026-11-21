# shortlist Specification

## Purpose
TBD - created by archiving change add-interest-and-shortlist. Update Purpose after archive.

## Requirements

### Requirement: Coach shortlist

The system SHALL let a signed-in coach add an athlete to a personal shortlist from the profile, view the shortlist at `/shortlist`, and remove athletes from it.

#### Scenario: Add and remove

- **GIVEN** a signed-in coach
- **WHEN** they toggle the shortlist on an athlete's profile
- **THEN** `/shortlist` contains that athlete
- **AND** toggling again removes the athlete from `/shortlist`

#### Scenario: Shortlist requires a coach

- **WHEN** an anonymous visitor requests `/shortlist`
- **THEN** the response redirects to `/demo`
