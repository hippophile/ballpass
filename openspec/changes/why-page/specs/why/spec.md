## ADDED Requirements

### Requirement: Why page tells the problem story

The system SHALL serve a public static-SSR page at `/why` titled "Γιατί Ballpass" (English "Why Ballpass") with six sections in
this order: hook, today, cost, with-ballpass, proof, doors. Greek copy uses the formal register; no "demo", "δείγμα" or "fictional"
word appears in the page content. Colour follows the flow rule: teal = the athlete's move, orange = the coach's move.

#### Scenario: Six sections in Greek and English
- **WHEN** a visitor requests `/why` in Greek (default) or with the English culture cookie
- **THEN** the page has the sections `hook`, `today`, `cost`, `how`, `proof`, `doors` in that order
- **AND** `<html lang>` matches the culture and the title is "Γιατί Ballpass" / "Why Ballpass"

#### Scenario: Both sides of today's chaos
- **WHEN** the "today" section renders
- **THEN** it shows the athlete side and the coach side and at least five fragments (forward, call, pdf, video, group)

#### Scenario: Four steps of the connection
- **WHEN** the "how" section renders
- **THEN** it shows four steps, coloured athlete, coach, athlete, connected, each with a preview of the app UI

#### Scenario: Live counters equal the database
- **WHEN** a visitor requests `/why`
- **THEN** the proof strip shows the number of demo athletes looking for a team, interests, and accepted interests, exactly as counted

#### Scenario: Every figure has a source
- **WHEN** the today or cost section shows a figure (a digit)
- **THEN** that figure sits inside an element marked as a fact that links to an external `https://` source

#### Scenario: Doors
- **WHEN** `Demo:Enabled=true`
- **THEN** the doors section has a coach form and an athlete form posting `/demo/login` with the featured personas

### Requirement: Why link in the footer and signed-out nav

The system SHALL link to `/why` from the footer on every page and from the signed-out navigation, and SHALL NOT add it to the
signed-in role navigation.

#### Scenario: Signed-out visitor
- **WHEN** a signed-out visitor requests `/`
- **THEN** the top navigation and the footer both link to `/why`

#### Scenario: Signed-in coach
- **WHEN** a signed-in coach requests a page
- **THEN** the top navigation has no `/why` link and the footer still has one
