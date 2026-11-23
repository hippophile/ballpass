## MODIFIED Requirements

### Requirement: Six post types render as typed cards

The system SHALL render looking-for-team, tryout call, stat update, highlight, badge and free-text posts as distinct cards, without JavaScript, and without any "(demo)", "δείγμα" or "seeded" label (SPEC §8 O1).

#### Scenario: Each type renders its own fields
- **WHEN** `/feed` is requested
- **THEN** every card shows an author chip and relative time, carries `data-post-type`, and has no `data-chip="demo"` chip
- **AND** each type shows its type-specific fields (looking-for-team: position, region, availability; tryout: date, place, positions, deadline; stat update: season, GP/PPG/RPG/APG plus source badge; highlight: its photos, or a stock photo from the feed pool when it has none; badge: badge name; free text: the text)

## ADDED Requirements

### Requirement: Three-column feed

`/feed` SHALL lay out at 1512 px in three columns `240px minmax(0,600px) 320px` with a 28 px gap (1216 px, the shell width): a sticky left rail with a "me" card (64 px jersey, name, role) and the persona's primary links (coach: Ταίριασμα, Λίστα, Αποστολές; athlete: Εισερχόμενα, Το προφίλ μου; anonymous: a card leading to `/demo`), the centre column (stories, composer, posts) and the right rail. The left rail hides below 1180 px; phones get one column. No per-page width override exists; the shell owns the width.

#### Scenario: Grid and rails
- **WHEN** `/feed` is requested
- **THEN** the page has the left rail, the centre column and the right rail, and the feed CSS defines `grid-template-columns: 240px minmax(0, 600px) 320px`
- **AND** a coach's left rail links to `/match`, `/shortlist` and `/me/sent`; an athlete's links to `/me/inbox` and their profile

