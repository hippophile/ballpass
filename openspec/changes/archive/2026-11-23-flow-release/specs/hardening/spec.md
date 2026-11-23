## MODIFIED Requirements

### Requirement: Correct pages for errors from form posts

The system SHALL render the access-denied page inside the normal layout (with the footer disclosure) when a non-coach posts a coach action.

#### Scenario: Athlete posts to shortlist

- **GIVEN** a signed-in athlete with a valid token
- **WHEN** they post to `/p/{slug}/shortlist`
- **THEN** the response status is 403
- **AND** the page shows the footer disclosure and the coaches-only message
