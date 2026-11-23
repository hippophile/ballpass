# share Specification

## Purpose
TBD - created by archiving change wow-share. Update Purpose after archive.

## Requirements

### Requirement: Open Graph share card

The system SHALL serve `GET /p/{slug}/og.png` anonymously as a 1200x630 PNG under 300 KB for every demo athlete, with solid top and bottom bands (at least 5% of the height each) reading "ΦΑΝΤΑΣΤΙΚΟ ΠΡΟΣΩΠΟ · FICTIONAL PERSON". The card SHALL NOT contain the word "DEMO" (no tiled watermark, no "(demo)" after the club name), because it travels without the site footer (SPEC §8 O1).

#### Scenario: PNG is exact and watermarked
- **WHEN** a client with no cookie requests `/p/{slug}/og.png`
- **THEN** the response is `image/png`, exactly 1200x630 and under 300 KB
- **AND** the top and bottom bands are the band colour and the renderer was given "ΦΑΝΤΑΣΤΙΚΟ ΠΡΟΣΩΠΟ · FICTIONAL PERSON"
- **AND** the card model's club line is the club name without "(demo)"

#### Scenario: Cache and conditional request
- **WHEN** the same card is requested twice
- **THEN** the second response is served from cache (no second render)
- **AND** a request with the returned `ETag` in `If-None-Match` gets 304

#### Scenario: Long Greek name
- **WHEN** an athlete has a very long Greek name with tonos
- **THEN** the card renders without throwing and without overflowing the canvas

#### Scenario: Unknown slug
- **WHEN** `/p/nobody/og.png` or `/p/nobody/season` is requested
- **THEN** the status is 404

### Requirement: Share meta tags on the profile

The profile head SHALL contain absolute `og:image`, `og:image:width=1200`, `og:image:height=630`, `og:image:alt` and `twitter:card=summary_large_image`; the base comes from config `PublicBaseUrl`, falling back to the request scheme and host.

#### Scenario: Tags present
- **WHEN** `/p/{slug}` is requested
- **THEN** `og:image` is an absolute URL ending `/p/{slug}/og.png`
- **AND** with `PublicBaseUrl` set, it starts with that base

### Requirement: Season Wrapped

The system SHALL serve `/p/{slug}/season` publicly without JavaScript as scroll-snap slides: intro, games and minutes, season stat line with source label, growth vs previous season, where she stands out in the demo cohort (n at least 20, else skipped), outro with `/p/{slug}?src=wrapped` and the card download.

#### Scenario: Numbers equal aggregates
- **WHEN** the page is rendered for an athlete with at least 5 games
- **THEN** every number equals its LINQ aggregate over the athlete's stat lines

#### Scenario: Few games
- **WHEN** the season has fewer than 5 games
- **THEN** only the intro, games/minutes and outro slides are shown

### Requirement: Card reflects looking-for-team

The share card's "Ψάχνει ομάδα" text SHALL follow the athlete's current `LookingForTeam`, and the card cache key SHALL change with it.

#### Scenario: Toggle
- **WHEN** an athlete without a club switches `LookingForTeam` off
- **THEN** the card model no longer contains "Ψάχνει ομάδα" and its hash and ETag differ

### Requirement: Wrapped note names the cohort

The wrapped cohort note SHALL mention the position group and the minutes floor, without "demo"/"επίδειξης"/"φανταστικά" wording.

#### Scenario: Note
- **WHEN** a forward's Season Wrapped shows the cohort slide
- **THEN** the note reads "...στην ίδια θέση (φόργουορντ), με τουλάχιστον 15 λεπτά ανά αγώνα" and contains no "επίδειξης"

### Requirement: Full story boundary

Season Wrapped SHALL show the full story from exactly `FullStoryMinGames` (5) games.

#### Scenario: Exactly 5 games
- **WHEN** a season has 4, 5 or 6 games
- **THEN** only 5 and 6 give the full story
