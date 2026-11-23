# feed Specification

## Purpose
TBD - created by archiving change wow-feed. Update Purpose after archive.

## Requirements

### Requirement: Six post types render as typed cards

The system SHALL render looking-for-team, tryout call, stat update, highlight, badge and free-text posts as distinct cards, without JavaScript, and without any "(demo)", "δείγμα" or "seeded" label (SPEC §8 O1).

#### Scenario: Each type renders its own fields
- **WHEN** `/feed` is requested
- **THEN** every card shows an author chip and relative time, carries `data-post-type`, and has no `data-chip="demo"` chip
- **AND** each type shows its type-specific fields (looking-for-team: position, region, availability; tryout: date, place, positions, deadline; stat update: season, GP/PPG/RPG/APG plus source badge; highlight: its photos, or a stock photo from the feed pool when it has none; badge: badge name; free text: the text)

### Requirement: Discover and Following tabs with keyset pagination

The system SHALL show a Discover feed (anonymous allowed, read-only) and a Following feed (signed in), 10 posts per page, "show more" via `?before=`, at most 60 posts in total, with no duplicates across pages.

#### Scenario: Paging
- **WHEN** a visitor follows "Show more" until it disappears
- **THEN** no post id repeats and at most 60 posts were shown

### Requirement: Posting permissions and limits

The system SHALL enforce server-side: athletes post free text, looking-for-team and highlight; coaches post tryout calls and free text; badges and stat updates are system-only; authors under 18 are rejected; 1 post per author per minute; free text at most 280 characters with URLs stripped; a POST without antiforgery token returns 400.

#### Scenario: Wrong role
- **WHEN** an athlete posts a tryout call, or a coach posts a highlight
- **THEN** the response is 403 and nothing is stored

#### Scenario: Rate limit
- **WHEN** the same persona posts twice within a minute
- **THEN** only the first post is stored

#### Scenario: Minor author
- **WHEN** the service is asked to create a post for an athlete under 18
- **THEN** it is rejected and nothing is stored

### Requirement: Kudos

The system SHALL offer one reaction ("kudos"), idempotent per post and actor, with the count visible to everyone, including anonymous visitors.

#### Scenario: Idempotent
- **WHEN** the same persona sends kudos "on" twice
- **THEN** exactly one kudos exists; a POST without token returns 400

#### Scenario: Public count
- **WHEN** the author, another persona and an anonymous visitor view a post with kudos
- **THEN** all three see the same count

### Requirement: Follow

The system SHALL let signed-in personas follow and unfollow authors, and a coach's shortlist action SHALL create a follow of that athlete.

#### Scenario: Shortlist follows
- **WHEN** a coach shortlists an athlete
- **THEN** a follow coach to athlete exists and following is idempotent

### Requirement: Safeguards

The system SHALL let a signed-in persona report (reason list), hide, or block an author; a report hides the post for the reporter at once and 3 distinct reports hide it for everyone; a block removes both parties from each other's feeds and follows.

#### Scenario: Report, hide, block
- **WHEN** a persona reports a post
- **THEN** it disappears for them immediately, and after 3 distinct reports for everyone
- **WHEN** a persona blocks an author
- **THEN** neither sees the other's posts and their follows are removed

### Requirement: Express interest from a looking-for-team card

The system SHALL show a plain form posting to `/p/{slug}/interest` and a profile link on a looking-for-team card to a signed-in coach, and neither to athletes or anonymous visitors.

#### Scenario: Roles
- **WHEN** a coach, an athlete and an anonymous visitor view a looking-for-team card
- **THEN** only the coach sees the interest form

### Requirement: Feed isolation

A failure in the feed services SHALL NOT break `/`, `/search` or `/p/{slug}`.

#### Scenario: Throwing feed service
- **WHEN** the feed services throw on every call
- **THEN** `/`, `/search` and `/p/{slug}` still return 200

### Requirement: Seed

The reset SHALL create 40 fictional posts (8 looking-for-team, 6 tryout, 10 stat update, 6 highlight, 4 badge, 6 free text) from 25 authors (18 athletes, 5 coaches, 2 clubs), 45 base kudos on 25 posts (max 5 per post), timed 6 within 24 h, 14 at 2-7 days, 20 at 8-30 days relative to the reset time. The engage seed raises kudos to about 250 in total.

#### Scenario: Counts and mix
- **WHEN** the feed seed is generated for a reference time
- **THEN** the counts, authors, timing and base kudos distribution above hold and every author is an adult

### Requirement: Home strip honours the viewer's filters

The home feed strip SHALL apply the same hide, block and report filters as `/feed` for the signed-in viewer.

#### Scenario: Blocked author
- **WHEN** a coach blocks the author of a post in the strip
- **THEN** the home strip no longer shows that author's posts

#### Scenario: Hidden post
- **WHEN** a coach hides a post shown in the strip
- **THEN** the home strip no longer shows that post

### Requirement: Relative time has one implementation

PostCard and the Sent page SHALL format relative time with one helper, wording "πριν από {n} λ./ώ./ημ.".

#### Scenario: Wording
- **WHEN** a time 5 minutes, 3 hours or 2 days ago is formatted
- **THEN** it reads "πριν από 5 λ.", "πριν από 3 ώ.", "πριν από 2 ημ."

### Requirement: Self-follow is rejected

The follow service and `/feed/follow` SHALL refuse a follow whose follower and target are the same actor.

#### Scenario: Self-follow
- **WHEN** an actor follows their own key, through the service or `/feed/follow`
- **THEN** the follow is refused and no row is created

### Requirement: Three-column feed

`/feed` SHALL lay out at 1512 px in three columns `240px minmax(0,600px) 320px` with a 28 px gap (1216 px, the shell width): a sticky left rail with a "me" card (64 px jersey, name, role) and the persona's primary links (coach: Ταίριασμα, Λίστα, Αποστολές; athlete: Εισερχόμενα, Το προφίλ μου; anonymous: a card leading to `/demo`), the centre column (stories, composer, posts) and the right rail. The left rail hides below 1180 px; phones get one column. No per-page width override exists; the shell owns the width.

#### Scenario: Grid and rails
- **WHEN** `/feed` is requested
- **THEN** the page has the left rail, the centre column and the right rail, and the feed CSS defines `grid-template-columns: 240px minmax(0, 600px) 320px`
- **AND** a coach's left rail links to `/match`, `/shortlist` and `/me/sent`; an athlete's links to `/me/inbox` and their profile
