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
