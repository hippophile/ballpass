# look

## MODIFIED Requirements

### Requirement: Role navigation with a user menu

The topbar SHALL show coaches exactly 4 primary links (Ταίριασμα, Λίστα, Αποστολές, Μηνύματα) and athletes exactly 3 (Εισερχόμενα, Μηνύματα, Το προφίλ μου); feed, search, the persona name and sign-out SHALL live in a `<details class="bp-usermenu">` whose summary is a 32 px jersey avatar. The persona switch pill and the EL|EN toggle stay visible.

#### Scenario: Coach nav
- **WHEN** a coach requests any page
- **THEN** the primary nav has 4 links: `/match`, `/shortlist`, `/me/sent`, `/messages`
- **AND** the user menu contains `/feed`, `/search` and the sign-out form

#### Scenario: Athlete nav
- **WHEN** an athlete requests any page
- **THEN** the primary nav has 3 links: `/me/inbox`, `/messages` and `/p/{own-slug}`
