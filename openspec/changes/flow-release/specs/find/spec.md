## MODIFIED Requirements

### Requirement: Match results explain themselves

The system SHALL show, per ranked athlete, templated reasons with source badges and an express-interest form.

#### Scenario: Reasons carry sources
- **WHEN** results are rendered
- **THEN** each result shows 2 to 3 templated reasons, each with a source badge, and an express-interest form posting to `/p/{slug}/interest`
- **AND** the page labels the score as computed with default weights, never calls it AI, and never says "demo" or "επίδειξης"

#### Scenario: Stat trust bar
- **WHEN** results are shown
- **THEN** the self-reported, coach-confirmed and league-verified (mock) shares sum to 100

### Requirement: Match uses the profile's cohort

The Match stat criterion and its "top X%" reason SHALL use the Evidence cohort rule: per-40 values, same gender and position group, peers with at least `Evidence:MinMinutesPerGame` and the minimum games, at least 20 peers, mid-rank percentile. When the rule yields no percentile the criterion SHALL count as missing ("partial data") and no top-% reason is shown.

#### Scenario: Not enough data
- **WHEN** an athlete's profile shows no percentile for rebounds
- **THEN** /match with priority rebounding shows no top-% reason for her and tags her "partial data"

#### Scenario: Same number
- **WHEN** an athlete has a rebounds percentile P on her profile
- **THEN** /match shows "top (100 - P)%" for her (tolerance 1)

#### Scenario: Wording
- **WHEN** a reason with a percentile is shown for a center
- **THEN** the reason reads "{value} ριμπ./40΄ — top {x}% των σέντερ" with no "(δείγμα επίδειξης)" suffix
