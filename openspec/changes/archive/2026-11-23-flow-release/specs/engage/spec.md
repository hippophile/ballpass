## MODIFIED Requirements

### Requirement: Engage seed

The reset SHALL create about 150 fictional comments by athletes and coaches (adults), about 250 kudos in total (some posts 20-40) and hashtags on about 40% of posts. `/feed` SHALL NOT show a "seeded activity" label (SPEC §8 O1); the footer disclosure covers it.

#### Scenario: Volumes
- **WHEN** the engage seed is generated
- **THEN** it has 150 comments, 230 to 270 kudos in total, and hashtags on 30% to 50% of the posts

#### Scenario: No seeded label
- **WHEN** `/feed` is requested
- **THEN** it contains no `data-engage="seeded"` element and no "seeded" or "Δείγμα δραστηριότητας" text

### Requirement: Two-column feed on desktop

The system SHALL show, from 1100 px, a sticky right rail on `/feed` headed "Ψάχνουν ομάδα τώρα" (teal) with 4 demo athletes who are looking for a team (jersey, name, position · region, link to the profile), followed by the trends box capped at 5 tags; who-to-follow leaves the rail. On phones the who-to-follow and trends boxes stay after the 3rd post.

#### Scenario: Layout hooks
- **WHEN** `/feed` is requested
- **THEN** the page contains the right rail and the inline boxes after the 3rd post (phone)
- **AND** the rail lists 4 athletes, each looking for a team and linking to their profile, and at most 5 trends, and no who-to-follow box
