## ADDED Requirements

### Requirement: Featured demo personas come from configuration

The system SHALL read the featured coach (`Demo:Featured:CoachFirstName`, `CoachLastName`), the featured athlete (`Demo:Featured:AthleteSlug`) and the featured need (`Demo:Featured:NeedQuery`) from configuration, with empty defaults in `appsettings.json` and the values in `appsettings.Development.json`; no page hardcodes them.

#### Scenario: Development values resolve to seeded personas
- **WHEN** the Development configuration is bound to `DemoOptions`
- **THEN** `Featured.CoachFirstName` is "Αικατερίνη", `Featured.CoachLastName` is "Φωτοπούλου" (SPEC §8 O7) and `Featured.AthleteSlug` is "danai-bakali"
- **AND** the seed-42 data contains exactly that coach and that athlete

#### Scenario: Defaults are empty
- **WHEN** `appsettings.json` is read
- **THEN** every `Demo:Featured` value is an empty string

### Requirement: Demo page leads with the featured personas

`/demo` SHALL show two featured persona cards with photos first (the featured coach, landing on `/match?need=featured` unless a `ReturnUrl` is given, and the featured athlete, landing on `/me/inbox` unless a `ReturnUrl` is given), then two compact lists side by side, coaches and athletes, each showing at most 8 rows before a `<details>` "Όλοι (n)" that holds the rest. Featured personas are not repeated in the lists. Every card and row is a working persona form.

#### Scenario: Featured cards
- **WHEN** a visitor requests `/demo`
- **THEN** the first coach form and the first athlete form are the featured personas from `Demo:Featured`, each inside a card with a photo
- **AND** posting the featured athlete form redirects to `/me/inbox`

#### Scenario: Compact lists
- **WHEN** a visitor requests `/demo`
- **THEN** each list shows at most 8 persona forms before its `<details>`, whose summary states the total count
