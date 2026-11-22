## MODIFIED Requirements

### Requirement: Percentile bars against a labelled demo cohort

The system SHALL show, on an athlete profile with stats, percentile bars for points, rebounds, assists, steals (per 40 minutes) and FG% against a comparison group of the same gender and position group, using each peer's latest season. Percentile SHALL be mid-rank, rounded and clamped to 1-99. The cohort label SHALL read "ομάδα σύγκρισης: N σέντερ" (position group name per athlete); no heading or label SHALL say "δείγμα επίδειξης", "demo" or "φανταστικά" (SPEC §8 O1).

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

#### Scenario: Label wording

- **WHEN** a center's profile is requested in Greek
- **THEN** the cohort label matches "ομάδα σύγκρισης: N σέντερ" and no percentile heading contains "δείγμα" or "επίδειξης"
