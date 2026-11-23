## ADDED Requirements

### Requirement: Seeded content in both languages

The demo seeders SHALL write every seeded text (post text, highlight title, tryout place and positions, comment, interest note, message,
athlete bio, club name) in Greek and in English. In English the system SHALL show the English text when present and the original
otherwise; content created at runtime SHALL show as typed. In Greek the system SHALL show the Greek text unchanged.

#### Scenario: English pages read in English
- **GIVEN** the culture cookie selects `en`
- **WHEN** a client requests `/feed` (coach), `/messages/{id}` (coach), `/me/inbox` (featured athlete), the featured profile or `/match?need=featured` (coach)
- **THEN** the visible text contains no Greek letter

#### Scenario: Runtime content shows as typed
- **GIVEN** the culture cookie selects `en`
- **WHEN** a coach sends the message "Γεια σου από το τεστ" in a seeded conversation
- **THEN** the thread shows "Γεια σου από το τεστ" and the seeded messages in English

#### Scenario: Greek unchanged
- **GIVEN** the culture cookie selects `el`
- **WHEN** a client requests `/messages/{id}` or the featured profile
- **THEN** the seeded Greek message bodies, bio and club names are shown

### Requirement: Hashtags per language with one key

A hashtag SHALL be stored as one canonical key; the system SHALL show its English label in English and its Greek label in Greek,
SHALL resolve an English or Greek tag in `/feed?tag=` to the same key, and trending SHALL count by key so both languages show the same counts.

#### Scenario: Trending consistent
- **WHEN** the trending box is rendered in `en` and in `el`
- **THEN** both list the same keys with the same counts, the English labels have no Greek letters

#### Scenario: English alias filters
- **WHEN** a client requests `/feed?tag=lookingforteam`
- **THEN** it shows the same posts as `/feed?tag=ψάχνωομάδα`

### Requirement: English place names

Club names SHALL have an English form (city exonym + translated nickname, e.g. "Larissa Halcyons") shown in English; city names SHALL show
as English exonyms (Athens, Piraeus, Patras, Heraklion, Larissa, Rhodes, ...). The rule parser SHALL map every seeded city to its region
in Greek and in English.

#### Scenario: Exonyms parse
- **WHEN** the rule parser reads the Greek name or the English exonym of any seeded city
- **THEN** it returns that city's region

### Requirement: Dates and ordinals per culture

Display dates SHALL come from one helper: English "Nov 22, 2026", "Sunday, Nov 1", "Connected since Nov 11"; Greek output unchanged.
Percentiles SHALL read "82nd percentile" in English (1st, 2nd, 3rd, 11th-13th, 21st, ...) and "82ο εκατοστημόριο" in Greek.

#### Scenario: Ordinals
- **WHEN** 1, 2, 3, 4, 11, 12, 13, 21, 22, 23, 82, 101, 111 are formatted in English
- **THEN** they read 1st, 2nd, 3rd, 4th, 11th, 12th, 13th, 21st, 22nd, 23rd, 82nd, 101st, 111th

#### Scenario: Dates
- **WHEN** 2026-11-22 is formatted in English
- **THEN** the date reads "Nov 22, 2026" and the day separator for 2026-11-01 reads "Sunday, Nov 1"
