## ADDED Requirements

### Requirement: Open Graph share card

The system SHALL serve `GET /p/{slug}/og.png` anonymously as a 1200x630 PNG under 300 KB for every demo athlete, with solid top and bottom watermark bands (at least 5% of the height each) reading "DEMO · ΦΑΝΤΑΣΤΙΚΟ ΠΡΟΣΩΠΟ · FICTIONAL ATHLETE" and a subtle tiled diagonal "DEMO".

#### Scenario: PNG is exact and watermarked
- **WHEN** a client with no cookie requests `/p/{slug}/og.png`
- **THEN** the response is `image/png`, exactly 1200x630 and under 300 KB
- **AND** the top and bottom bands are the band colour and the renderer was given the watermark text

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
