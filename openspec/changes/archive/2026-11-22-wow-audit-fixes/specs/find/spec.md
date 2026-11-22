## ADDED Requirements

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
