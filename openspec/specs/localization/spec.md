# localization Specification

## Purpose
TBD - created by archiving change add-profile-and-i18n. Update Purpose after archive.

## Requirements

### Requirement: Greek by default, English on request

The system SHALL render pages in Greek by default and in English when the culture cookie selects `en`, and SHALL set the `<html lang>` attribute to the active language.

#### Scenario: Default Greek

- **WHEN** a client without a culture cookie requests a profile
- **THEN** the `<html>` element has `lang="el"`

#### Scenario: English selected

- **GIVEN** the culture cookie selects `en`
- **WHEN** the client requests a profile
- **THEN** the `<html>` element has `lang="en"`
- **AND** the athlete's name is shown in Latin characters
- **AND** a coach-confirmed stat line shows the label "Coach-confirmed"

### Requirement: Safe language switch

The system MUST accept only `el` and `en` on `/culture/set` and MUST redirect only to local paths.

#### Scenario: Switch and return

- **WHEN** a client requests `/culture/set?culture=en&redirectUri=/p/some-slug`
- **THEN** the response is a redirect to `/p/some-slug`
- **AND** a culture cookie for `en` is set

#### Scenario: Open redirect blocked

- **WHEN** a client requests `/culture/set?culture=en&redirectUri=https://evil.example/`
- **THEN** the response is a redirect to `/`

### Requirement: Formal plural Greek register

Greek resources of Feed, Loop, Evidence, Find and Share SHALL address the user in the formal plural ("Ψάχνετε", "στείλατε", "Διαλέξτε").

#### Scenario: No singular imperative
- **WHEN** the five Greek resource sets are scanned
- **THEN** none contains a known singular form (Δες, Βρες, Διάλεξε, Ακολούθησε, Συνδέσου, Περίμενε, Δοκίμασε, Συμπλήρωσε, Πρόσθεσε, ...)
