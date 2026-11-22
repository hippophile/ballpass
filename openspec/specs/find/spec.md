# find Specification

## Purpose
TBD - created by archiving change wow-find. Update Purpose after archive.

## Requirements

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
- **THEN** the reason reads "{value} ριμπ./40΄ — top {x}% των σέντερ (δείγμα επίδειξης)"

### Requirement: The model client never retries

`AnthropicClaudeClient` SHALL configure `MaxRetries = 0`.

#### Scenario: One request per failure
- **WHEN** the API answers HTTP 500
- **THEN** exactly one request reached the server and the caller gets `AiUnavailableException`

### Requirement: Robust model call

The JSON schema SHALL describe every property and express nullable fields with `anyOf` ([typed, null]); `Ai:TimeoutSeconds` SHALL default to 10; every fallback to the rule parser SHALL log its reason (refusal, max_tokens, timeout, http status, invalid JSON, disabled, cap) at Information without the query text.

#### Scenario: Schema
- **WHEN** the schema is read
- **THEN** every property has a non-empty description, `q` says "ONLY a person's name", `looking` says the athlete is looking for a team, and no property uses a type array or `null` inside `enum`

#### Scenario: Logged reason
- **WHEN** the model times out
- **THEN** one Information log entry names the reason and does not contain the query

### Requirement: Rule parser positions and minors

"πόιντ γκαρντ" / "ποιντ γκαρντ" SHALL map to PG only and "σούτινγκ γκαρντ" to SG only. An upper age below 18 SHALL clamp `minAge` to 18, leave `maxAge` empty and keep the "minors" chip.

#### Scenario: Under 16
- **WHEN** the query is "γυναίκες κάτω των 16"
- **THEN** minAge=18, no maxAge, the minors chip is present and /search returns results

### Requirement: Bad query values never fail the page

/match and /search SHALL parse query values leniently (invalid = absent) and strip control characters from `q`.

#### Scenario: Garbage
- **WHEN** `minHeight=abc`, `minAge=abc`, `confirmed=maybe`, `looking=maybe`, `minHeight=999999999999`, `q=%00` or `page=abc` is requested
- **THEN** the status is 200

### Requirement: One unsupported key

The AI prompt and the UI SHALL use the key `stats`; every key the validator can emit SHALL have a Greek label.

#### Scenario: Labels
- **WHEN** the Greek resource is read for every key in `SearchFilterValidator.UnsupportedKeys`
- **THEN** each of `SearchFilterValidator.UnsupportedKeys` has an `Unsupported_{key}` entry in the Greek resource

### Requirement: Singular counts

`Results_Count` and `Results_Hidden` SHALL use singular wording for exactly 1.

#### Scenario: One result
- **WHEN** a /match page has exactly one result and one hidden athlete
- **THEN** the page reads "1 αποτέλεσμα" / "1 κρυμμένος"
