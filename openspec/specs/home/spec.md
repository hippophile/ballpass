# home Specification

## Purpose
TBD - created by archiving change polish-home-and-mobile. Update Purpose after archive.

## Requirements

### Requirement: Usable on phones

The system SHALL render every MVP page without horizontal page overflow at 390px width and SHALL collapse the top-bar links into a menu on narrow screens.

#### Scenario: Narrow screen

- **GIVEN** a 390px wide browser
- **WHEN** any MVP page is opened
- **THEN** the document scroll width does not exceed the viewport width

### Requirement: Home page opens two doors

The system SHALL serve a public home page whose first screen is a full-bleed hero video (poster only on phones and under reduced motion) and two photo doors, one per side of the market: a coach door that signs in as the featured coach and lands on `/match` with the featured need, and an athlete door that signs in as the featured athlete and lands on `/me/inbox`. Featured personas and the need come from `Demo:Featured` configuration (coach resolved by first and last name, athlete by slug); nothing is hardcoded. Below the doors the page explains the connection in three steps, shows live counts from the database, the latest accepted connections and recent feed activity. No demo label or caption is shown (SPEC §8 O1); Greek copy uses the formal register (O2).

#### Scenario: Two doors post the demo login
- **WHEN** a visitor requests `/` with `Demo:Enabled=true`
- **THEN** the page has exactly two door forms posting `/demo/login`
- **AND** the coach door carries the featured coach id and `returnUrl` `/match?need=featured` (/match's shortcut for `Demo:Featured:NeedQuery`)
- **AND** the athlete door carries the featured athlete id and `returnUrl` `/me/inbox`

#### Scenario: Doors without the demo
- **WHEN** `Demo:Enabled=false`
- **THEN** both doors are links to `/demo` and no form posts `/demo/login`

#### Scenario: Hero video
- **WHEN** a visitor requests `/`
- **THEN** the hero renders the `HeroVideo` component and the old text hero, "Try as a coach" CTA, sample-profile CTA, steps grid and trust list are gone

#### Scenario: Live counts equal the database
- **WHEN** a visitor requests `/`
- **THEN** the three counters show the number of demo athletes looking for a team, the number of interests and the number of accepted interests, exactly as counted in the database, with no "δεδομένα επίδειξης" caption

#### Scenario: Recent connections
- **WHEN** at least one interest is accepted
- **THEN** the page lists the latest accepted interests, at most 6
- **AND** when none is accepted the section is not rendered

#### Scenario: Activity cards have a photo header
- **WHEN** the home activity strip renders
- **THEN** each card has a photo header picked from the stock feed pool by post id

#### Scenario: Both languages
- **GIVEN** the culture cookie selects `en`
- **WHEN** a visitor requests `/`
- **THEN** the coach door button reads "Find a player"

### Requirement: Not-found page leads back into the flow

The not-found page SHALL show a photo band with a scrim, the title "Δεν βρέθηκε" and one button: back to `/match` for a coach, back to `/feed` for everyone else.

#### Scenario: Anonymous visitor
- **WHEN** an anonymous visitor requests a missing page
- **THEN** the page shows the not-found stock photo and a button to `/feed`

#### Scenario: Coach
- **WHEN** a signed-in coach requests a missing page
- **THEN** the button goes to `/match`

### Requirement: Home shows the problem band

The system SHALL show, on the home page right after the two persona doors, a compact "Το πρόβλημα" band with two columns,
"Σήμερα" (three ✕ rows) and "Με το Ballpass" (three ✓ rows), and a link "Δείτε την ιστορία →" to `/why`. The doors stay the first
content after the hero.

#### Scenario: Band after the doors
- **WHEN** a visitor requests `/`
- **THEN** the band follows both doors and precedes the connection band in the markup
- **AND** it has three rows in each column and links to `/why`
