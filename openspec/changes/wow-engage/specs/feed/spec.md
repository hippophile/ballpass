## MODIFIED Requirements

### Requirement: Kudos

The system SHALL offer one reaction ("kudos"), idempotent per post and actor, with the count visible to everyone, including anonymous visitors.

#### Scenario: Idempotent
- **WHEN** the same persona sends kudos "on" twice
- **THEN** exactly one kudos exists; a POST without token returns 400

#### Scenario: Public count
- **WHEN** the author, another persona and an anonymous visitor view a post with kudos
- **THEN** all three see the same count

### Requirement: Seed

The reset SHALL create 40 fictional posts (8 looking-for-team, 6 tryout, 10 stat update, 6 highlight, 4 badge, 6 free text) from 25 authors (18 athletes, 5 coaches, 2 clubs), 45 base kudos on 25 posts (max 5 per post), timed 6 within 24 h, 14 at 2-7 days, 20 at 8-30 days relative to the reset time. The engage seed raises kudos to about 250 in total.

#### Scenario: Counts and mix
- **WHEN** the feed seed is generated for a reference time
- **THEN** the counts, authors, timing and base kudos distribution above hold and every author is an adult
