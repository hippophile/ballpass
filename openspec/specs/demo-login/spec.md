# demo-login Specification

## Purpose
TBD - created by archiving change add-demo-login-and-search. Update Purpose after archive.

## Requirements

### Requirement: Demo persona login

The system SHALL let a visitor sign in as a demo coach or demo athlete from `/demo` without a password, using antiforgery-protected forms, and SHALL let them sign out.

#### Scenario: Coach signs in

- **WHEN** a visitor submits the first coach persona form on `/demo`
- **THEN** the response redirects to `/search`
- **AND** subsequent pages show the coach's name in the top bar

#### Scenario: Open redirect blocked on login

- **WHEN** a visitor submits a persona form with `returnUrl=https://evil.example/`
- **THEN** the response does not redirect to the external host

#### Scenario: Sign out

- **GIVEN** a signed-in coach
- **WHEN** they post to `/demo/logout`
- **THEN** requesting `/search` redirects to `/demo`

### Requirement: Search is for coaches only

The system MUST allow `/search` only for signed-in coaches; anonymous visitors MUST be sent to `/demo` and signed-in athletes MUST receive a 403 "coaches only" page.

#### Scenario: Anonymous visitor

- **WHEN** an anonymous visitor requests `/search`
- **THEN** the response redirects to `/demo` with a return URL

#### Scenario: Athlete denied

- **GIVEN** a signed-in athlete
- **WHEN** they request `/search` and follow the redirect
- **THEN** the final response status is 403

### Requirement: Featured demo personas come from configuration

The system SHALL read the featured coach (`Demo:Featured:CoachFirstName`, `CoachLastName`), the featured athlete (`Demo:Featured:AthleteSlug`) and the featured need (`Demo:Featured:NeedQuery`) from configuration, with empty defaults in `appsettings.json` and the values in `appsettings.Development.json`; no page hardcodes them.

#### Scenario: Development values resolve to seeded personas
- **WHEN** the Development configuration is bound to `DemoOptions`
- **THEN** `Featured.CoachFirstName` is "Αικατερίνη", `Featured.CoachLastName` is "Φωτοπούλου" (SPEC §8 O7) and `Featured.AthleteSlug` is "danai-bakali"
- **AND** the seed-42 data contains exactly that coach and that athlete

#### Scenario: Defaults are empty
- **WHEN** `appsettings.json` is read
- **THEN** every `Demo:Featured` value is an empty string

### Requirement: Demo page leads with the featured personas

`/demo` SHALL show two featured persona cards with photos first (the featured coach, landing on `/match?need=featured` unless a `ReturnUrl` is given, and the featured athlete, landing on `/me/inbox` unless a `ReturnUrl` is given), then two compact lists side by side, coaches and athletes, each showing at most 8 rows before a `<details>` "Όλοι (n)" that holds the rest. Featured personas are not repeated in the lists. Every card and row is a working persona form.

#### Scenario: Featured cards
- **WHEN** a visitor requests `/demo`
- **THEN** the first coach form and the first athlete form are the featured personas from `Demo:Featured`, each inside a card with a photo
- **AND** posting the featured athlete form redirects to `/me/inbox`

#### Scenario: Compact lists
- **WHEN** a visitor requests `/demo`
- **THEN** each list shows at most 8 persona forms before its `<details>`, whose summary states the total count
