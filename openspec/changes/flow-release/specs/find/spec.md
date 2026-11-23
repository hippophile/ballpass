## MODIFIED Requirements

### Requirement: Match results explain themselves

The system SHALL show, per ranked athlete, templated reasons with their source and an express-interest form; the top match links to the profile instead (flow-release §4.5).

#### Scenario: Reasons carry sources
- **WHEN** results are rendered
- **THEN** each result shows 1 to 3 templated reasons, each carrying its source (`data-source`; the stat reason also shows a source badge)
- **AND** every result except the top match has an outline express-interest form posting to `/p/{slug}/interest`
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

## ADDED Requirements

### Requirement: Featured need ranks the featured athlete first

`/match?need=featured` SHALL apply the query in `Demo:Featured:NeedQuery` (same parameter names as the Match form). With the Development values, the athlete `Demo:Featured:AthleteSlug` (`ioanna-papageorgiou`, the O7 swap after the lead review: her profile clears the minutes floor and shows percentile bars) SHALL rank first at 100 % with no tie at the top.

#### Scenario: Featured need
- **WHEN** a coach opens `/match?{NeedQuery}` or `/match?need=featured`
- **THEN** the first card is the featured athlete with `data-top="true"` and fit 100, and the second card scores below 100

### Requirement: No empty rings

A fit ring SHALL only render with a score. Without any scoring criterion the page SHALL show no ring and one prompt card ("Πείτε μας τι χρειάζεστε → θέση, ύψος, περιοχή") with a link to `/match?need=featured`.

#### Scenario: Bare match
- **WHEN** a coach opens `/match` with no query
- **THEN** no `.bp-find-fit` element exists, no ring shows "–", and the prompt card (`data-match-prompt`) is present

### Requirement: Match layout and one orange CTA

`/match` SHALL show a header need chip bar, the AI box with three example chips (GET `/find/ai?to=match&q=…`), the filters (sticky; a "Φίλτρα (n)" `<details>` below 900 px), a top-match card (stock no-face poster `StockMedia.MatchTop`, 96 px ring, up to 3 full reasons, the teal "Ψάχνει ομάδα" chip) whose "Δείτε το προφίλ →" is the page's only `.bp-btn-primary`, then 12 cards in two columns and "Περισσότερα" (`?take=24`). Card actions are `.bp-btn-outline`; a sent interest shows a teal "✓ Στάλθηκε" pill.

#### Scenario: One primary
- **WHEN** the featured need is shown
- **THEN** the page has exactly one `.bp-btn-primary`, inside `.bp-match-top`, linking to the featured profile, and no `.bp-looking` or flat `.bp-btn`

#### Scenario: Paging
- **WHEN** a ranked list has more than 13 results
- **THEN** the page shows the top card + 12 cards and a link with `take=24`; with `take=24` it shows the top card + 24

### Requirement: Search reuses the card

`/search` SHALL render its results as `AthleteMatchCard` (variant `search`, no ring) in the same 2-column grid, with the teal `.bp-chip-athlete` for LookingForTeam.

#### Scenario: Search cards
- **WHEN** a coach opens `/search?looking=true`
- **THEN** cards have `data-variant="search"`, there is no `.bp-find-fit`, and the teal chip is shown
