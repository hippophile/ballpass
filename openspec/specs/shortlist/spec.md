# shortlist Specification

## Purpose
TBD - created by archiving change add-interest-and-shortlist. Update Purpose after archive.

## Requirements

### Requirement: Coach shortlist

The system SHALL let a signed-in coach add an athlete to a personal shortlist from the profile, view the shortlist at `/shortlist`, and remove athletes from it.

#### Scenario: Add and remove

- **GIVEN** a signed-in coach
- **WHEN** they toggle the shortlist on an athlete's profile
- **THEN** `/shortlist` contains that athlete
- **AND** toggling again removes the athlete from `/shortlist`

#### Scenario: Shortlist requires a coach

- **WHEN** an anonymous visitor requests `/shortlist`
- **THEN** the response redirects to `/demo`

### Requirement: Shortlist card grid

`/shortlist` SHALL show the entries as `AthleteMatchCard` (variant `shortlist`: jersey 56, three per-game stat chips, teal LookingForTeam chip) in an auto-fill grid beside a sticky compare panel (the existing compare launcher). The first card whose interest is not sent SHALL carry the page's only `.bp-btn-primary` "Εκδήλωση ενδιαφέροντος"; other cards use outline; a sent interest shows a teal "✓ Στάλθηκε" pill; each card has a ghost "Αφαίρεση" (POST `/p/{slug}/shortlist`).

#### Scenario: Grid
- **WHEN** a coach with 3 or more entries opens `/shortlist`
- **THEN** every card has `data-variant="shortlist"`, 3 stat chips and a ghost remove button, and the page has exactly one `.bp-btn-primary`

#### Scenario: Sent
- **WHEN** the coach has sent an interest to a shortlisted athlete
- **THEN** her card shows "✓ Στάλθηκε" and no interest form

### Requirement: Empty shortlist

An empty shortlist SHALL show a 240 px photo band (`StockMedia.ShortlistEmpty`) with "Η λίστα σας είναι άδεια" and an orange "Βρείτε αθλήτριες →" to `/match`.

#### Scenario: Empty
- **WHEN** a coach with no entries opens `/shortlist`
- **THEN** the band, the text and one `.bp-btn-primary` linking to `/match` are shown
