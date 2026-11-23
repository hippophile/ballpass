# localization Specification

## Purpose
TBD - created by archiving change add-profile-and-i18n. Update Purpose after archive.

## Requirements

### Requirement: Safe language switch

The system MUST accept only `el` and `en` on `/culture/set` and MUST redirect only to local paths.

#### Scenario: Switch and return

- **WHEN** a client requests `/culture/set?culture=en&redirectUri=/p/some-slug`
- **THEN** the response is a redirect to `/p/some-slug`
- **AND** a culture cookie for `en` is set

#### Scenario: Open redirect blocked

- **WHEN** a client requests `/culture/set?culture=en&redirectUri=https://evil.example/`
- **THEN** the response is a redirect to `/`

### Requirement: Formal plural Greek register

Greek resources of Feed, Loop, Evidence, Find and Share SHALL address the user in the formal plural ("Ψάχνετε", "στείλατε", "Διαλέξτε").

#### Scenario: No singular imperative
- **WHEN** the five Greek resource sets are scanned
- **THEN** none contains a known singular form (Δες, Βρες, Διάλεξε, Ακολούθησε, Συνδέσου, Περίμενε, Δοκίμασε, Συμπλήρωσε, Πρόσθεσε, ...)

### Requirement: Configurable default language, English out of the box

The system SHALL render pages in the culture configured by `Localization:DefaultCulture` (supported: `el`, `en`; shipped value `en`)
when no culture cookie is present, SHALL ignore the browser's `Accept-Language` header, SHALL render Greek when the culture cookie
selects `el`, and SHALL set the `<html lang>` attribute to the active language.

#### Scenario: Default English
- **GIVEN** the shipped configuration
- **WHEN** a client without a culture cookie requests `/`, `/why`, `/demo`, `/feed`, `/match`, the featured profile, `/messages` or `/me/inbox`
- **THEN** the `<html>` element has `lang="en"` and the page chrome is English

#### Scenario: Browser language ignored
- **GIVEN** the shipped configuration
- **WHEN** a client without a culture cookie sends `Accept-Language: el`
- **THEN** the `<html>` element has `lang="en"`

#### Scenario: Greek selected
- **GIVEN** the culture cookie selects `el` (set by `/culture/set?culture=el`)
- **WHEN** the client requests a page
- **THEN** the `<html>` element has `lang="el"` and the page chrome is Greek

#### Scenario: English selected shows Latin names
- **GIVEN** the culture cookie selects `en`
- **WHEN** the client requests a profile
- **THEN** the athlete's name is shown in Latin characters
- **AND** a coach-confirmed stat line shows the label "Coach-confirmed"

#### Scenario: Greek default by configuration
- **GIVEN** `Localization:DefaultCulture` is `el`
- **WHEN** a client without a culture cookie requests a profile
- **THEN** the `<html>` element has `lang="el"`

### Requirement: Complete English resources

Every resource key SHALL exist in both the Greek and the English file, and no English value SHALL contain Greek letters
(identical values are allowed only for language-neutral tokens: brand, numbers, stat abbreviations, placeholders).
English values SHALL NOT use the words "demo", "sample" or "fictional" except in the footer disclosure and the /demo intro.

#### Scenario: Keys in both languages
- **WHEN** every `*.resx` / `*.en.resx` pair is compared
- **THEN** both files have the same set of keys

#### Scenario: No Greek in English
- **WHEN** every `*.en.resx` value is scanned
- **THEN** none contains a Greek letter

#### Scenario: No demo words
- **WHEN** every `*.en.resx` value is scanned
- **THEN** only `Footer_Disclosure` and `Demo_Intro` contain "demo", "sample" or "fictional"

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
