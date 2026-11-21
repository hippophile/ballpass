# hardening Specification

## Purpose
TBD - created by archiving change fix-audit-findings. Update Purpose after archive.

## Requirements

### Requirement: Redirects stay on this site

The system MUST redirect only to local paths that start with a single `/`, are not protocol-relative, and contain no control characters, on every endpoint that accepts a return or redirect URL.

#### Scenario: Tab-character bypass blocked

- **WHEN** a client requests `/culture/set?culture=en&redirectUri=/%09/evil.example`
- **THEN** the response redirects to `/`

#### Scenario: Line-break input does not crash

- **WHEN** a client requests `/culture/set?culture=en&redirectUri=/%0d%0a/x`
- **THEN** the response redirects to `/`

#### Scenario: Login return URL with control characters

- **WHEN** a persona form is submitted with `returnUrl` set to `/` + TAB + `/evil.example`
- **THEN** the response does not redirect to `evil.example`

### Requirement: Antiforgery on every state-changing request

The system MUST reject state-changing POST requests without a valid antiforgery token with HTTP 400 and MUST NOT respond with a server error.

#### Scenario: Shortlist without token

- **GIVEN** a signed-in coach
- **WHEN** they post to `/p/{slug}/shortlist` without a token
- **THEN** the response status is 400

#### Scenario: Logout without token

- **GIVEN** a signed-in coach
- **WHEN** they post to `/demo/logout` without a token
- **THEN** the response status is 400
- **AND** they are still signed in

#### Scenario: Invalid token on interest

- **WHEN** a signed-in coach posts an interest with an invalid token
- **THEN** the response status is 400

### Requirement: Stale sessions after a demo reset

The system SHALL sign out a user whose persona no longer exists after a demo reset instead of failing.

#### Scenario: Coach session survives a reset safely

- **GIVEN** a signed-in coach
- **WHEN** the demo data is reset and the coach requests `/search`
- **THEN** the response redirects to `/demo`

### Requirement: Correct pages for errors from form posts

The system SHALL render the access-denied page with the demo banner when a non-coach posts a coach action.

#### Scenario: Athlete posts to shortlist

- **GIVEN** a signed-in athlete with a valid token
- **WHEN** they post to `/p/{slug}/shortlist`
- **THEN** the response status is 403
- **AND** the page shows the demo banner and the coaches-only message

### Requirement: Duplicate submissions do not fail

The system SHALL store at most one interest per coach and athlete even when the same interest is posted several times at once, without server errors.

#### Scenario: Parallel duplicate posts

- **WHEN** a coach posts the same interest 6 times in parallel
- **THEN** no response has status 500
- **AND** exactly one interest exists for that pair
