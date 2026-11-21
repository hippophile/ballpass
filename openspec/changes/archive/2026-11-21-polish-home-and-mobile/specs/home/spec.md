## ADDED Requirements

### Requirement: Investor-ready home page

The system SHALL serve a public home page that explains Ballpass, links to the demo login and to a sample athlete profile, and shows live demo counts.

#### Scenario: Calls to action

- **WHEN** a visitor requests `/`
- **THEN** the page has a link with `data-cta="demo"` to `/demo`
- **AND** a link with `data-cta="sample"` to an existing athlete profile that returns 200

#### Scenario: Live demo counts

- **WHEN** a visitor requests `/`
- **THEN** the page shows the number of demo athletes (300) and clubs (20)

#### Scenario: Both languages

- **GIVEN** the culture cookie selects `en`
- **WHEN** a visitor requests `/`
- **THEN** the call to action reads "Try as a coach"

### Requirement: Usable on phones

The system SHALL render every MVP page without horizontal page overflow at 390px width and SHALL collapse the top-bar links into a menu on narrow screens.

#### Scenario: Narrow screen

- **GIVEN** a 390px wide browser
- **WHEN** any MVP page is opened
- **THEN** the document scroll width does not exceed the viewport width
