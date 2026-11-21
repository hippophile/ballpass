## ADDED Requirements

### Requirement: Fit scoring is deterministic and renormalised

The system SHALL compute a fit percentage per athlete from only the criteria the coach requested, with weights from `Fit:*` configuration, per-game statistics, and a statistic with fewer than 10 games treated as missing.

#### Scenario: Worked examples
- **WHEN** the three documented fixtures are scored
- **THEN** the order and percentages equal the fixture expectations

#### Scenario: Missing data
- **WHEN** a requested statistic is missing for an athlete
- **THEN** the score is renormalised over the available criteria
- **AND** the athlete is tagged "partial data" and loses ties on coverage

#### Scenario: Tie-breaks
- **WHEN** two athletes have the same fit percentage
- **THEN** they order by coverage, then best stat source, then most recent season, then slug

### Requirement: Hard filters hide and are counted

The system SHALL hide athletes excluded by a position filter or the coach-confirmed-only toggle and SHALL show how many were hidden.

#### Scenario: Hidden count
- **WHEN** a position or the coach-confirmed-only toggle excludes athletes
- **THEN** they are not listed and "N hidden by filters" shows the count

### Requirement: Match results explain themselves

The system SHALL show, per ranked athlete, templated reasons with source badges and an express-interest form.

#### Scenario: Reasons carry sources
- **WHEN** results are rendered
- **THEN** each result shows 2 to 3 templated reasons, each with a source badge, and an express-interest form posting to `/p/{slug}/interest`
- **AND** the page labels the score as computed on fictional demo data with default weights and never calls it AI

#### Scenario: Stat trust bar
- **WHEN** results are shown
- **THEN** the self-reported, coach-confirmed and league-verified (mock) shares sum to 100

### Requirement: Match is coach-only

The system SHALL restrict `/match` to the Coach role.

#### Scenario: Access
- **WHEN** an anonymous visitor requests `/match`
- **THEN** they are redirected to `/demo`
- **AND** an athlete requesting `/match` receives the 403 page

### Requirement: Natural-language search never depends on an AI model

The system SHALL convert a natural-language query into search filters with a rule parser that is always available, and MAY use a model parser behind a safety rail.

#### Scenario: Rules path
- **WHEN** a Greek or English query is parsed by rules
- **THEN** the expected filters are returned, accent-insensitively, and the UI says "From rules (no AI model)"

#### Scenario: Validation
- **WHEN** a parser returns out-of-range or unknown values
- **THEN** minAge is clamped to at least 18, unknown enum values are dropped, and a prompt-injection query yields empty filters

#### Scenario: Fallback chain
- **WHEN** the model call throws, times out, refuses, hits max_tokens, or returns invalid JSON
- **THEN** the rule parser result is used, with no retry

#### Scenario: Safety rail
- **WHEN** an identical normalised query repeats
- **THEN** zero further API calls are made
- **AND** when the per-session cap is reached, `Ai:Enabled` is false, or no key is configured, no API call is made

#### Scenario: Redirect
- **WHEN** a coach requests `GET /find/ai?q=...`
- **THEN** it redirects to `/search?...` carrying the filters

### Requirement: No secrets in git

The repository SHALL contain no Anthropic API key.

#### Scenario: Key pattern
- **WHEN** tracked files are scanned
- **THEN** none contains the Anthropic key prefix
