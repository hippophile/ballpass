## REMOVED Requirements

### Requirement: Demo banner on every page

**Reason**: Founder 2026-11-22 (SPEC §8 O1): "don't say demo or image demo, we know it, it's killing the vibe". The banner is replaced by one quiet footer disclosure.

**Migration**: Use the requirement "One fictional-data disclosure" below; tests assert the banner's absence and the footer line's presence.

## ADDED Requirements

### Requirement: One fictional-data disclosure

The system SHALL render on every HTML page exactly one footer line, 12 px in `--bp-faint`, reading "Φανταστικά πρόσωπα και δεδομένα" (Greek) or "Fictional people and data" (English), and SHALL NOT render any other "demo", "δείγμα", "seeded" or "fictional" label in visible text, except the `/demo` intro sentence.

#### Scenario: Home page shows the footer line
- **WHEN** a client requests `GET /`
- **THEN** the response status is 200
- **AND** the HTML contains `<footer class="bp-footer">` with "Φανταστικά πρόσωπα και δεδομένα"
- **AND** the HTML contains no element with class `demo-banner`

#### Scenario: Not-found page shows the footer line
- **WHEN** a client requests a path that does not exist
- **THEN** the HTML still contains the footer disclosure and no demo banner

#### Scenario: No demo words in visible text
- **WHEN** `/`, `/feed`, `/match` (coach), `/p/{slug}`, `/shortlist` (coach), `/me/inbox` (athlete) or `/me/sent` (coach) is requested in `el` or `en`
- **THEN** the visible text (tags, scripts, styles and attributes stripped) outside the footer contains none of "demo", "δείγμα", "seeded", "επίδειξ" (case-insensitive)
