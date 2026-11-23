## ADDED Requirements

### Requirement: Home shows the problem band

The system SHALL show, on the home page right after the two persona doors, a compact "Το πρόβλημα" band with two columns,
"Σήμερα" (three ✕ rows) and "Με το Ballpass" (three ✓ rows), and a link "Δείτε την ιστορία →" to `/why`. The doors stay the first
content after the hero.

#### Scenario: Band after the doors
- **WHEN** a visitor requests `/`
- **THEN** the band follows both doors and precedes the connection band in the markup
- **AND** it has three rows in each column and links to `/why`
