## ADDED Requirements

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
