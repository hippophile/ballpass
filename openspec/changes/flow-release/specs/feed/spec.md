## MODIFIED Requirements

### Requirement: Six post types render as typed cards

The system SHALL render looking-for-team, tryout call, stat update, highlight, badge and free-text posts as distinct cards, without JavaScript, and without any "(demo)", "δείγμα" or "seeded" label (SPEC §8 O1).

#### Scenario: Each type renders its own fields
- **WHEN** `/feed` is requested
- **THEN** every card shows an author chip and relative time, carries `data-post-type`, and has no `data-chip="demo"` chip
- **AND** each type shows its type-specific fields (looking-for-team: position, region, availability; tryout: date, place, positions, deadline; stat update: season, GP/PPG/RPG/APG plus source badge; highlight: generated SVG art with a text equivalent; badge: badge name; free text: the text)
