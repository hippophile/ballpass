## REMOVED Requirements

### Requirement: Greek by default, English on request

**Reason**: Founder decision 2026-11-23: English is the default UI language.
**Migration**: Replaced by "Configurable default language, English out of the box". Hosts that need Greek by default set
`Localization:DefaultCulture` to `el`.

## ADDED Requirements

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
