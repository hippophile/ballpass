## ADDED Requirements

### Requirement: Reset needs a signed-in persona and is rate limited

`POST /demo/reset` SHALL refuse anonymous callers by redirecting to `/demo` without resetting anything, SHALL allow one reset per minute per signed-in persona (further requests answer 429 with a normal page), and SHALL sign the caller out on success. `Demo:Enabled` SHALL default to false in `appsettings.json` and be true in `appsettings.Development.json`; when false the endpoint answers 404.

#### Scenario: Anonymous reset
- **WHEN** an anonymous client posts `/demo/reset` with a valid antiforgery token
- **THEN** the response redirects to `/demo` and the seeder is not called

#### Scenario: Second reset within a minute
- **WHEN** the same signed-in persona posts `/demo/reset` twice within a minute
- **THEN** the first redirects to `/?demo=reset` and the second answers 429 with a page; the seeder ran once

#### Scenario: Caller is signed out
- **WHEN** a signed-in persona resets and the persona row still exists afterwards
- **THEN** the response clears the auth cookie and the next request is anonymous

#### Scenario: Defaults
- **WHEN** configuration is read from `appsettings.json` and from the Development test host
- **THEN** `Demo:Enabled` is false in the first and true in the second

### Requirement: Persona claims are built in one place

The `/demo/login` endpoint and the persona switch SHALL build claims through the same `PersonaClaims` helper, and every state-changing POST SHALL validate antiforgery through the same helper.

#### Scenario: Same shape
- **WHEN** a coach logs in and when a coach switches to the athlete view
- **THEN** both principals are built by `PersonaClaims` (login and switch tests stay green)
