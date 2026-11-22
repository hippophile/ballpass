## MODIFIED Requirements

### Requirement: Engage seed

The reset SHALL create about 150 fictional comments by athletes and coaches (adults), about 250 kudos in total (some posts 20-40) and hashtags on about 40% of posts. `/feed` SHALL NOT show a "seeded activity" label (SPEC §8 O1); the footer disclosure covers it.

#### Scenario: Volumes
- **WHEN** the engage seed is generated
- **THEN** it has 150 comments, 230 to 270 kudos in total, and hashtags on 30% to 50% of the posts

#### Scenario: No seeded label
- **WHEN** `/feed` is requested
- **THEN** it contains no `data-engage="seeded"` element and no "seeded" or "Δείγμα δραστηριότητας" text
