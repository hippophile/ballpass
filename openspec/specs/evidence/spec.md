# evidence Specification

## Purpose
TBD - created by archiving change wow-evidence. Update Purpose after archive.

## Requirements

### Requirement: Percentile bars against a labelled demo cohort

The system SHALL show, on an athlete profile with stats, percentile bars for points, rebounds, assists, steals (per 40 minutes) and FG% against a demo cohort of the same gender and position group, using each peer's latest season. Percentile SHALL be mid-rank, rounded and clamped to 1-99. Every heading of percentile content SHALL contain "δείγμα επίδειξης" and the cohort label SHALL read "δείγμα επίδειξης: N σέντερ (φανταστικά)" (position group name per athlete).

#### Scenario: Bar geometry

- **WHEN** the percentile is 78
- **THEN** the fill is 156 of a 200 unit track
- **AND** the SVG has `role="img"`, an `aria-label` with the value, and a visually hidden table gives the same numbers

#### Scenario: Not enough data

- **WHEN** the cohort has fewer than 20 peers or the athlete has fewer than 10 games
- **THEN** no fill is drawn and the text "Δεν υπάρχουν αρκετά δεδομένα (n=...)" is shown

#### Scenario: Coach-confirmed-only

- **WHEN** the profile is requested with `?confirmed=1`
- **THEN** only peers whose latest stat line is coach-confirmed are in the cohort and the cohort size changes

### Requirement: Season progression with trend

The system SHALL draw points per game across seasons as an inline SVG line. A missing season SHALL break the line. The trend SHALL be ▲ at +10% or more, ▼ at -10% or less, ▬ otherwise, comparing the latest season with the immediately previous one, and "n/a" when either has fewer than 8 games or the previous season is missing.

#### Scenario: Trend rule

- **WHEN** the latest points per game are 11.0 and the previous are 10.0 (both with 8+ games)
- **THEN** the trend is ▲
- **AND** 9.0 gives ▼, 10.5 gives ▬, 7 games gives "n/a"

### Requirement: Rule badges tied to source labels

The system SHALL show rule-based badges whose text names the rule (for example "Επιβεβαιωμένο από προπονητή: 2 σεζόν"): coach-confirmed seasons, league-verified (mock) seasons, 3+ seasons of stats, 25+ games in the latest season, upward trend, top quartile in points per 40 within the cohort.

#### Scenario: Each rule

- **WHEN** an athlete meets or misses a rule's condition
- **THEN** the badge is present or absent accordingly

### Requirement: Compare two athletes

The system SHALL offer `/compare?a={slug}&b={slug}` to coaches only. Anonymous visitors are redirected to `/demo`, athletes get 403. The page SHALL show both athletes' percentile bars with different marker shapes and dash patterns, a table whose numbers equal the SVG labels, and a warning when the position groups differ.

#### Scenario: Access

- **WHEN** an anonymous visitor requests `/compare`
- **THEN** the response redirects to `/demo`
- **AND** an athlete persona ends on a 403 page

#### Scenario: Parity

- **WHEN** a coach compares two athletes
- **THEN** every percentile in the table equals the matching SVG label

### Requirement: Invariant-culture SVG and chart colours

All numbers in SVG attributes SHALL use the invariant culture. `--bp-chart-a..e` SHALL have at least 3:1 contrast on #0B0F14.

#### Scenario: Greek culture

- **WHEN** a profile is requested in `el`
- **THEN** `points=` and coordinates use dots, never commas

### Requirement: Compare warns about different genders

`/compare` SHALL warn when the two athletes are of different gender, naming both, because the cohorts are separate.

#### Scenario: Female vs male
- **WHEN** /compare shows a female and a male athlete
- **THEN** the warning names the different gender and both gender labels

### Requirement: Cohort percentile batch

`ICohortService.PercentilesAsync(gender, metric)` SHALL return, per athlete id, the same whole-percent mid-rank percentile the profile shows, and omit athletes for whom the profile shows none.

#### Scenario: Parity with the profile
- **WHEN** the batch and the profile are computed for every female athlete and metric
- **THEN** the values are equal, and an athlete without a profile percentile is absent from the batch
