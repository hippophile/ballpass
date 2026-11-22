## ADDED Requirements

### Requirement: Featured demo personas come from configuration

The system SHALL read the featured coach (`Demo:Featured:CoachFirstName`, `CoachLastName`), the featured athlete (`Demo:Featured:AthleteSlug`) and the featured need (`Demo:Featured:NeedQuery`) from configuration, with empty defaults in `appsettings.json` and the values in `appsettings.Development.json`; no page hardcodes them.

#### Scenario: Development values resolve to seeded personas
- **WHEN** the Development configuration is bound to `DemoOptions`
- **THEN** `Featured.CoachFirstName` is "Ευτυχία", `Featured.CoachLastName` is "Σωτηρίου" and `Featured.AthleteSlug` is "danai-bakali"
- **AND** the seed-42 data contains exactly that coach and that athlete

#### Scenario: Defaults are empty
- **WHEN** `appsettings.json` is read
- **THEN** every `Demo:Featured` value is an empty string
