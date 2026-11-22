## MODIFIED Requirements

### Requirement: Post media renders without layout shift and with an honest label

Every media item SHALL render with `width`/`height`, a localized `alt`, `loading="lazy"` and `decoding="async"`. It SHALL NOT carry a visible "demo" chip (SPEC §8 O1); the alt text never claims to show the named athlete. Anonymous, athlete and coach SHALL all see the same markup.

#### Scenario: Photo markup
- **WHEN** a post with a photo is rendered
- **THEN** the `img` has width, height, non-empty alt and lazy loading, and no "Εικόνα αρχείου · demo" chip is present

#### Scenario: Video markup
- **WHEN** a post with a video is rendered
- **THEN** the `video` has `muted playsinline preload="none"` and a `poster`, and no `autoplay` attribute
- **AND** it keeps native `controls` and no "Γραφικό · demo" chip is present

#### Scenario: Carousel
- **WHEN** a post has 2-4 photos
- **THEN** they render in a scroll-snap list with one dot link per photo; a single photo has no dots

## ADDED Requirements

### Requirement: Stock media slots

Stock media SHALL live under `wwwroot/media/stock/` with published names, be listed with source URL, author, licence and a `name` in `stock/credits.json`, and be referenced only through `Features/Media/StockMedia.cs` slot constants and pools. Slots shown next to a named fictional athlete (profile cover pool, highlights, match top card) SHALL use only files whose credits entry has no faces.

#### Scenario: Every slot resolves to a credited file
- **WHEN** every `StockMedia` constant and pool entry is resolved
- **THEN** the file exists under `wwwroot/media/stock/` and has a `credits.json` entry with `source_page_url`, `author`, `licence` and `name`

#### Scenario: No faces next to named athletes
- **WHEN** the cover pool, the highlight slots and the match top-card poster are checked against `credits.json`
- **THEN** every one has `faces: false`

#### Scenario: Excluded file never ships
- **WHEN** the stock folder is listed
- **THEN** it contains no file derived from `pexels-40732461` and the feed post pool has 10 entries

### Requirement: Stock photo and hero video components

`StockPhoto` SHALL render an `img` (width, height, `loading="lazy"`, `decoding="async"`, or `fetchpriority="high"` when marked as priority) plus a scrim layer and no chip. `HeroVideo` SHALL render `<video muted autoplay loop playsinline preload="metadata" poster=…>` whose only `<source>` carries `media="(min-width:768px) and (prefers-reduced-motion:no-preference)"`.

#### Scenario: Hero video only on wide screens without reduced motion
- **WHEN** `HeroVideo` is rendered
- **THEN** the `source` has that media query and the poster is the hero poster
