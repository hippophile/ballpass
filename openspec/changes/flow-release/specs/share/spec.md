## MODIFIED Requirements

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

### Requirement: Wrapped note names the cohort

The wrapped cohort note SHALL mention the position group and the minutes floor, without "demo"/"επίδειξης"/"φανταστικά" wording.

#### Scenario: Note
- **WHEN** a forward's Season Wrapped shows the cohort slide
- **THEN** the note reads "...στην ίδια θέση (φόργουορντ), με τουλάχιστον 15 λεπτά ανά αγώνα" and contains no "επίδειξης"
